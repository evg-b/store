using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Evg.Store {
	public sealed class StoreService<TState> : IStoreService<TState> {
		private TState _state;
		private readonly Reducer<TState> _reducer;
		private readonly List<Action<TState>> _listeners = new List<Action<TState>>();
		private readonly DiagnosticTarget _diagnosticTarget;
		private readonly long _storeId;
		private long _sequence;
		private long _nextSubscriptionId;

		public StoreService(TState initialState, Reducer<TState> reducer){
			_state = initialState;
			_reducer = reducer ?? throw new ArgumentNullException(nameof(reducer));
			_storeId = StoreDiagnostics.AllocateStoreId();
			_diagnosticTarget = new DiagnosticTarget(this);
			StoreDiagnostics.RegisterStore(_storeId, _diagnosticTarget);

			if (StoreDiagnostics.HasSubscribers) {
				PublishDiagnostic(StoreOperation.Created, null, null, _state, true, TimeSpan.Zero, null);
			}
		}

		public void SetStore(TState newState) {
			TState previousState = _state;
			_state = newState;

			if (StoreDiagnostics.HasSubscribers) {
				_sequence++;
				PublishDiagnostic(
					StoreOperation.StateReplaced,
					null,
					previousState,
					newState,
					!Equals(previousState, newState),
					TimeSpan.Zero,
					null
				);
			}
		}

		public TState GetStore() {
			return _state;
		}

		public void Dispatch(IAction action){
			if (action == null) throw new ArgumentNullException(nameof(action));

			TState previousState = _state;
			StoreDispatchMeasurement parentDispatch = StoreDiagnostics.CurrentDispatch;
			bool eventDiagnosticsEnabled = StoreDiagnostics.HasSubscribers;
			bool pipelineDiagnosticsEnabled = StoreDiagnostics.HasDispatchSubscribers || StoreDiagnostics.HasSubscriptionSubscribers;
			bool diagnosticsEnabled = eventDiagnosticsEnabled || pipelineDiagnosticsEnabled || parentDispatch != null;
			long sequence = diagnosticsEnabled ? ++_sequence : 0;
			StoreDispatchMeasurement dispatch = diagnosticsEnabled
				? new StoreDispatchMeasurement {
					StoreId = _storeId,
					Sequence = sequence,
					Depth = parentDispatch == null ? 0 : parentDispatch.Depth + 1,
					WallStartedAt = Stopwatch.GetTimestamp(),
					Action = action,
					Parent = parentDispatch,
					CapturePipeline = pipelineDiagnosticsEnabled || (parentDispatch?.CapturePipeline ?? false)
				}
				: null;
			StoreDiagnostics.CurrentDispatch = dispatch;

			try {
				TState newState;
				long reducerStartedAt = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0;
				long nestedWallBefore = dispatch?.NestedWallTicks ?? 0;
				try {
					newState = _reducer(previousState, action);
					if (dispatch != null) dispatch.ReducerTicks += ExclusiveElapsedTicks(reducerStartedAt, nestedWallBefore, dispatch);
				} catch (Exception exception) {
					if (dispatch != null) {
						dispatch.ReducerTicks += ExclusiveElapsedTicks(reducerStartedAt, nestedWallBefore, dispatch);
						dispatch.FailurePhase = StoreDispatchFailurePhase.Reducer;
						dispatch.Error = exception;
					}
					if (eventDiagnosticsEnabled) {
						PublishDiagnostic(
							StoreOperation.Dispatch,
							action,
							previousState,
							previousState,
							false,
							DurationFromStopwatchTicks(dispatch?.ReducerTicks ?? 0),
							exception,
							dispatch
						);
					}
					throw;
				}

				bool stateChanged = !Equals(previousState, newState);
				if (stateChanged) _state = newState;

				// Начало Action публикуется до listener-ов: вложенные Dispatch сохраняют причинный порядок.
				if (eventDiagnosticsEnabled) {
					PublishDiagnostic(
						StoreOperation.Dispatch,
						action,
						previousState,
						newState,
						stateChanged,
						DurationFromStopwatchTicks(dispatch?.ReducerTicks ?? 0),
						null,
						dispatch
					);
				}

				if (stateChanged) NotifyListeners();
			} catch (Exception exception) {
				if (dispatch != null && dispatch.Error == null) {
					dispatch.FailurePhase = StoreDispatchFailurePhase.Listener;
					dispatch.Error = exception;
				}
				throw;
			} finally {
				CompleteDispatch(dispatch);
			}
		}

		public IDisposable Subscribe(Action<TState> listener){
			return SubscribeCore(
				state => state,
				listener,
				EqualityComparer<TState>.Default,
				StoreSubscriptionKind.State,
				"State"
			);
		}

		public IDisposable Subscribe<TSelected>(
			Func<TState, TSelected> selector,
			Action<TSelected> listener,
			IEqualityComparer<TSelected> comparer = null
		){
			if (selector == null) throw new ArgumentNullException(nameof(selector));
			if (listener == null) throw new ArgumentNullException(nameof(listener));

			return SubscribeCore(
				selector,
				listener,
				comparer ?? EqualityComparer<TSelected>.Default,
				StoreSubscriptionKind.Selector,
				DelegateName(selector)
			);
		}

		private IDisposable SubscribeCore<TSelected>(
			Func<TState, TSelected> selector,
			Action<TSelected> listener,
			IEqualityComparer<TSelected> equalityComparer,
			StoreSubscriptionKind kind,
			string selectorName
		){
			if (selector == null) throw new ArgumentNullException(nameof(selector));
			if (listener == null) throw new ArgumentNullException(nameof(listener));

			long subscriptionId = ++_nextSubscriptionId;
			string listenerName = DelegateName(listener);
			string listenerTargetType = listener.Target?.GetType().FullName;
			string comparerName = equalityComparer.GetType().FullName;
			object listenerTarget = listener.Target;
			bool diagnosticsEnabled = StoreDiagnostics.HasSubscriptionSubscribers;
			string sourceFilePath = null;
			int sourceLineNumber = 0;
			if (diagnosticsEnabled) CaptureSubscriptionSource(out sourceFilePath, out sourceLineNumber);
			long startedAt = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0;
			TSelected selectedState;
			TimeSpan initialSelectorDuration;

			try {
				selectedState = selector(_state);
				initialSelectorDuration = diagnosticsEnabled ? ElapsedSince(startedAt) : TimeSpan.Zero;
			} catch (Exception exception) {
				if (diagnosticsEnabled) {
					PublishSubscriptionDiagnostic(
						subscriptionId, kind, StoreSubscriptionOperation.Created,
						selectorName, listenerName, listenerTargetType, comparerName, listenerTarget,
						sourceFilePath, sourceLineNumber,
						ElapsedSince(startedAt), TimeSpan.Zero, false, false, exception
					);
				}
				throw;
			}

			void OnStateChanged(TState state){
				StoreDispatchMeasurement activeDispatch = StoreDiagnostics.CurrentDispatch;
				bool captureSubscriptionEvent = StoreDiagnostics.HasSubscriptionSubscribers;
				StoreDispatchMeasurement metricsDispatch = captureSubscriptionEvent || (activeDispatch?.CapturePipeline ?? false)
					? activeDispatch
					: null;
				bool captureTiming = captureSubscriptionEvent || metricsDispatch != null;
				long selectorStartedAt = captureTiming ? Stopwatch.GetTimestamp() : 0;
				long selectorNestedWallBefore = metricsDispatch?.NestedWallTicks ?? 0;
				TSelected newSelectedState = default;
				bool selectedValueChanged = false;
				try {
					newSelectedState = selector(state);
					selectedValueChanged = !equalityComparer.Equals(selectedState, newSelectedState);
				} catch (Exception exception) {
					long failedSelectorTicks = captureTiming
						? ExclusiveElapsedTicks(selectorStartedAt, selectorNestedWallBefore, metricsDispatch)
						: 0;
					RecordSelectorMetrics(metricsDispatch, failedSelectorTicks, false, exception);
					if (captureSubscriptionEvent) {
						PublishSubscriptionDiagnostic(
							subscriptionId, kind, StoreSubscriptionOperation.Evaluated,
							selectorName, listenerName, listenerTargetType, comparerName, listenerTarget,
							sourceFilePath, sourceLineNumber,
							DurationFromStopwatchTicks(failedSelectorTicks), TimeSpan.Zero, false, false, exception,
							activeDispatch
						);
					}
					throw;
				}

				long selectorTicks = captureTiming
					? ExclusiveElapsedTicks(selectorStartedAt, selectorNestedWallBefore, metricsDispatch)
					: 0;
				RecordSelectorMetrics(metricsDispatch, selectorTicks, selectedValueChanged, null);
				TimeSpan selectorDuration = DurationFromStopwatchTicks(selectorTicks);
				if (!selectedValueChanged) {
					if (captureSubscriptionEvent) {
						PublishSubscriptionDiagnostic(
							subscriptionId, kind, StoreSubscriptionOperation.Evaluated,
							selectorName, listenerName, listenerTargetType, comparerName, listenerTarget,
							sourceFilePath, sourceLineNumber,
							selectorDuration, TimeSpan.Zero, false, false, null,
							activeDispatch
						);
					}
					return;
				}

				// Значение обновляется до listener, потому что listener может синхронно вызвать следующий Dispatch.
				selectedState = newSelectedState;
				long listenerStartedAt = captureTiming ? Stopwatch.GetTimestamp() : 0;
				long listenerNestedWallBefore = metricsDispatch?.NestedWallTicks ?? 0;
				try {
					listener(newSelectedState);
				} catch (Exception exception) {
					long listenerTicks = captureTiming
						? ExclusiveElapsedTicks(listenerStartedAt, listenerNestedWallBefore, metricsDispatch)
						: 0;
					RecordListenerMetrics(metricsDispatch, listenerTicks, exception);
					if (captureSubscriptionEvent) {
						PublishSubscriptionDiagnostic(
							subscriptionId, kind, StoreSubscriptionOperation.Evaluated,
							selectorName, listenerName, listenerTargetType, comparerName, listenerTarget,
							sourceFilePath, sourceLineNumber,
							selectorDuration, DurationFromStopwatchTicks(listenerTicks), true, true, exception,
							activeDispatch
						);
					}
					throw;
				}

				long completedListenerTicks = captureTiming
					? ExclusiveElapsedTicks(listenerStartedAt, listenerNestedWallBefore, metricsDispatch)
					: 0;
				RecordListenerMetrics(metricsDispatch, completedListenerTicks, null);
				if (captureSubscriptionEvent) {
					PublishSubscriptionDiagnostic(
						subscriptionId, kind, StoreSubscriptionOperation.Evaluated,
						selectorName, listenerName, listenerTargetType, comparerName, listenerTarget,
						sourceFilePath, sourceLineNumber,
						selectorDuration, DurationFromStopwatchTicks(completedListenerTicks), true, true, null,
						activeDispatch
					);
				}
			}

			_listeners.Add(OnStateChanged);

			TimeSpan initialListenerDuration;
			long initialListenerStartedAt = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0;
			try {
				listener(selectedState);
				initialListenerDuration = diagnosticsEnabled ? ElapsedSince(initialListenerStartedAt) : TimeSpan.Zero;
			} catch (Exception exception) {
				initialListenerDuration = diagnosticsEnabled ? ElapsedSince(initialListenerStartedAt) : TimeSpan.Zero;
				_listeners.Remove(OnStateChanged);
				if (diagnosticsEnabled) {
					PublishSubscriptionDiagnostic(
						subscriptionId, kind, StoreSubscriptionOperation.Created,
						selectorName, listenerName, listenerTargetType, comparerName, listenerTarget,
						sourceFilePath, sourceLineNumber,
						initialSelectorDuration, initialListenerDuration, true, true, exception
					);
				}
				throw;
			}

			if (diagnosticsEnabled) {
				PublishSubscriptionDiagnostic(
						subscriptionId, kind, StoreSubscriptionOperation.Created,
						selectorName, listenerName, listenerTargetType, comparerName, listenerTarget,
						sourceFilePath, sourceLineNumber,
						initialSelectorDuration, initialListenerDuration, true, true, null
					);
			}

			return new Unsubscribe(
				this,
				OnStateChanged,
				subscriptionId,
				kind,
				selectorName,
				listenerName,
				listenerTargetType,
				comparerName,
				listenerTarget,
				sourceFilePath,
				sourceLineNumber
			);
		}

		private void NotifyListeners(){
			// Снимок позволяет listener безопасно подписываться и отписываться во время уведомления.
			Action<TState>[] listeners = _listeners.ToArray();
			foreach (Action<TState> listener in listeners) {
				listener(_state);
			}
		}

		private object GetStateForDiagnostics(){
			return _state;
		}

		private bool TryReplaceStateForDiagnostics(object state, out string error){
			if (!TryCastDiagnosticState(state, out TState typedState, out error)) return false;
			TState previousState = _state;
			bool stateChanged = !Equals(previousState, typedState);
			_state = typedState;

			// Time Travel публикуется до listener-ов: вложенные Dispatch продолжают иметь однозначный порядок.
			if (StoreDiagnostics.HasSubscribers) {
				_sequence++;
				PublishDiagnostic(
					StoreOperation.TimeTravel,
					null,
					previousState,
					typedState,
					stateChanged,
					TimeSpan.Zero,
					null
				);
			}

			if (stateChanged) NotifyListeners();
			error = null;
			return true;
		}

		private static bool TryCastDiagnosticState(object state, out TState typedState, out string error){
			if (state == null && typeof(TState).IsValueType && Nullable.GetUnderlyingType(typeof(TState)) == null) {
				typedState = default;
				error = $"State cannot be null for {typeof(TState).FullName}.";
				return false;
			}

			if (state != null && !(state is TState)) {
				typedState = default;
				error = $"State type {state.GetType().FullName} is incompatible with {typeof(TState).FullName}.";
				return false;
			}

			typedState = state == null ? default : (TState)state;
			error = null;
			return true;
		}

		private bool UnsubscribeListener(Action<TState> listener){
			return _listeners.Remove(listener);
		}

		private static TimeSpan ElapsedSince(long startedAt){
			long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
			return DurationFromStopwatchTicks(elapsedTicks);
		}

		private static long ExclusiveElapsedTicks(
			long startedAt,
			long nestedWallBefore,
			StoreDispatchMeasurement dispatch
		){
			long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
			long nestedTicks = dispatch == null ? 0 : Math.Max(0, dispatch.NestedWallTicks - nestedWallBefore);
			return Math.Max(0, elapsedTicks - nestedTicks);
		}

		private static TimeSpan DurationFromStopwatchTicks(long stopwatchTicks){
			double timeSpanTicks = Math.Max(0, stopwatchTicks) *
			                       (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency);
			return TimeSpan.FromTicks((long)Math.Round(timeSpanTicks));
		}

		private static void RecordSelectorMetrics(
			StoreDispatchMeasurement dispatch,
			long selectorTicks,
			bool selectedValueChanged,
			Exception error
		){
			if (dispatch == null) return;
			dispatch.SelectorTicks += selectorTicks;
			dispatch.SubscriptionEvaluations++;
			if (!selectedValueChanged && error == null) dispatch.ComparerSkips++;
			if (error == null || dispatch.Error != null) return;

			dispatch.FailurePhase = StoreDispatchFailurePhase.Selector;
			dispatch.Error = error;
		}

		private static void RecordListenerMetrics(StoreDispatchMeasurement dispatch, long listenerTicks, Exception error){
			if (dispatch == null) return;
			dispatch.ListenerTicks += listenerTicks;
			dispatch.SubscriptionNotifications++;
			if (error == null || dispatch.Error != null) return;

			dispatch.FailurePhase = StoreDispatchFailurePhase.Listener;
			dispatch.Error = error;
		}

		private void CompleteDispatch(StoreDispatchMeasurement dispatch){
			if (dispatch == null) {
				StoreDiagnostics.CurrentDispatch = null;
				return;
			}

			StoreDiagnostics.CurrentDispatch = dispatch.Parent;
			long ownTicks = dispatch.ReducerTicks + dispatch.SelectorTicks + dispatch.ListenerTicks;
			long totalTicks = ownTicks + dispatch.NestedDispatchTicks;
			if (StoreDiagnostics.HasDispatchSubscribers) {
				StoreDiagnostics.PublishDispatchCompleted(new StoreDispatchDiagnosticEvent(
					_storeId,
					typeof(TState).Name,
					typeof(TState),
					dispatch.Sequence,
					dispatch.Parent?.StoreId ?? -1,
					dispatch.Parent?.Sequence ?? -1,
					dispatch.Depth,
					DateTimeOffset.UtcNow,
					dispatch.Action,
					DurationFromStopwatchTicks(dispatch.ReducerTicks),
					DurationFromStopwatchTicks(dispatch.SelectorTicks),
					DurationFromStopwatchTicks(dispatch.ListenerTicks),
					DurationFromStopwatchTicks(ownTicks),
					DurationFromStopwatchTicks(dispatch.NestedDispatchTicks),
					DurationFromStopwatchTicks(totalTicks),
					dispatch.SubscriptionEvaluations,
					dispatch.SubscriptionNotifications,
					dispatch.ComparerSkips,
					dispatch.FailurePhase,
					dispatch.Error
				));
			}

			if (dispatch.Parent == null) return;
			// Родитель вычитает полный wall time дочернего Dispatch из listener-а и добавляет только измеренную работу Store.
			dispatch.Parent.NestedWallTicks += Math.Max(0, Stopwatch.GetTimestamp() - dispatch.WallStartedAt);
			dispatch.Parent.NestedDispatchTicks += totalTicks;
		}

		private void PublishDiagnostic(
			StoreOperation operation,
			IAction action,
			object previousState,
			object currentState,
			bool stateChanged,
			TimeSpan duration,
			Exception error,
			StoreDispatchMeasurement dispatch = null
		){
			StoreDiagnostics.Publish(new StoreDiagnosticEvent(
				_storeId,
				typeof(TState).Name,
				typeof(TState),
				operation,
				dispatch?.Sequence ?? _sequence,
				dispatch?.Parent?.StoreId ?? -1,
				dispatch?.Parent?.Sequence ?? -1,
				dispatch?.Depth ?? 0,
				DateTimeOffset.UtcNow,
				action,
				previousState,
				currentState,
				stateChanged,
				duration,
				error
			));
		}

		private void PublishSubscriptionDiagnostic(
			long subscriptionId,
			StoreSubscriptionKind kind,
			StoreSubscriptionOperation operation,
			string selectorName,
			string listenerName,
			string listenerTargetType,
			string comparerName,
			object listenerTarget,
			string sourceFilePath,
			int sourceLineNumber,
			TimeSpan selectorDuration,
			TimeSpan listenerDuration,
			bool selectedValueChanged,
			bool listenerNotified,
			Exception error,
			StoreDispatchMeasurement dispatch = null
		){
			StoreDiagnostics.PublishSubscription(new StoreSubscriptionDiagnosticEvent(
				_storeId,
				subscriptionId,
				typeof(TState).Name,
				typeof(TState),
				kind,
				operation,
				dispatch?.Sequence ?? 0,
				dispatch?.Depth ?? 0,
				DateTimeOffset.UtcNow,
				selectorName,
				listenerName,
				listenerTargetType,
				comparerName,
				listenerTarget,
				sourceFilePath,
				sourceLineNumber,
				selectorDuration,
				listenerDuration,
				selectedValueChanged,
				listenerNotified,
				error
			));
		}

		private static string DelegateName(Delegate value){
			if (value == null) return null;
			Type declaringType = value.Method.DeclaringType;
			return declaringType == null ? value.Method.Name : $"{declaringType.FullName}.{value.Method.Name}";
		}

		private static void CaptureSubscriptionSource(out string sourceFilePath, out int sourceLineNumber){
			sourceFilePath = null;
			sourceLineNumber = 0;

			try {
				StackFrame[] frames = new StackTrace(true).GetFrames();
				if (frames == null) return;

				foreach (StackFrame frame in frames) {
					Type declaringType = frame.GetMethod()?.DeclaringType;
					if (IsStoreServiceFrame(declaringType)) continue;

					string candidatePath = frame.GetFileName();
					int candidateLine = frame.GetFileLineNumber();
					if (string.IsNullOrWhiteSpace(candidatePath) || candidateLine <= 0) continue;

					sourceFilePath = candidatePath;
					sourceLineNumber = candidateLine;
					return;
				}
			} catch {
				// Навигация к исходнику — вспомогательная диагностика и не должна влиять на создание подписки.
			}
		}

		private static bool IsStoreServiceFrame(Type type){
			while (type != null) {
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(StoreService<>)) return true;
				type = type.DeclaringType;
			}
			return false;
		}

		private sealed class Unsubscribe : IDisposable {
			private readonly StoreService<TState> _store;
			private readonly Action<TState> _listener;
			private readonly long _subscriptionId;
			private readonly StoreSubscriptionKind _kind;
			private readonly string _selectorName;
			private readonly string _listenerName;
			private readonly string _listenerTargetType;
			private readonly string _comparerName;
			private readonly object _listenerTarget;
			private readonly string _sourceFilePath;
			private readonly int _sourceLineNumber;
			private bool _disposed;

			public Unsubscribe(
				StoreService<TState> store,
				Action<TState> listener,
				long subscriptionId,
				StoreSubscriptionKind kind,
				string selectorName,
				string listenerName,
				string listenerTargetType,
				string comparerName,
				object listenerTarget,
				string sourceFilePath,
				int sourceLineNumber
			){
				_store = store;
				_listener = listener;
				_subscriptionId = subscriptionId;
				_kind = kind;
				_selectorName = selectorName;
				_listenerName = listenerName;
				_listenerTargetType = listenerTargetType;
				_comparerName = comparerName;
				_listenerTarget = listenerTarget;
				_sourceFilePath = sourceFilePath;
				_sourceLineNumber = sourceLineNumber;
			}

			public void Dispose(){
				if (_disposed) return;
				_disposed = true;
				if (!_store.UnsubscribeListener(_listener) || !StoreDiagnostics.HasSubscriptionSubscribers) return;
				_store.PublishSubscriptionDiagnostic(
					_subscriptionId,
					_kind,
					StoreSubscriptionOperation.Disposed,
					_selectorName,
					_listenerName,
					_listenerTargetType,
					_comparerName,
					_listenerTarget,
					_sourceFilePath,
					_sourceLineNumber,
					TimeSpan.Zero,
					TimeSpan.Zero,
					false,
					false,
					null
				);
			}
		}

		private sealed class DiagnosticTarget : IStoreDiagnosticTarget {
			private readonly StoreService<TState> _store;

			public DiagnosticTarget(StoreService<TState> store){
				_store = store;
			}

			public Type StateType => typeof(TState);

			public object GetState(){
				return _store.GetStateForDiagnostics();
			}

			public bool TryReplaceState(object state, out string error){
				return _store.TryReplaceStateForDiagnostics(state, out error);
			}
		}
	}
}
