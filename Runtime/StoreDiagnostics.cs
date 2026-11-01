using System;
using System.Collections.Generic;
using System.Threading;

namespace Evg.Store {
	internal interface IStoreDiagnosticTarget {
		Type StateType { get; }
		object GetState();
		bool TryReplaceState(object state, out string error);
	}

	public static class StoreDiagnostics {
		private static long _nextStoreId;
		private static readonly object TargetsLock = new object();
		private static readonly Dictionary<long, WeakReference<IStoreDiagnosticTarget>> Targets =
			new Dictionary<long, WeakReference<IStoreDiagnosticTarget>>();
		[ThreadStatic] private static StoreDispatchMeasurement _currentDispatch;

		public static event Action<StoreDiagnosticEvent> EventPublished;
		public static event Action<StoreDispatchDiagnosticEvent> DispatchCompleted;
		public static event Action<StoreSubscriptionDiagnosticEvent> SubscriptionEventPublished;

		internal static bool HasSubscribers => EventPublished != null;
		internal static bool HasDispatchSubscribers => DispatchCompleted != null;
		internal static bool HasSubscriptionSubscribers => SubscriptionEventPublished != null;
		internal static StoreDispatchMeasurement CurrentDispatch {
			get => _currentDispatch;
			set => _currentDispatch = value;
		}

		internal static long AllocateStoreId(){
			return Interlocked.Increment(ref _nextStoreId);
		}

		internal static void RegisterStore(long storeId, IStoreDiagnosticTarget target){
			if (target == null) throw new ArgumentNullException(nameof(target));
			lock (TargetsLock) Targets[storeId] = new WeakReference<IStoreDiagnosticTarget>(target);
		}

		public static bool TryGetState(long storeId, out object state, out Type stateType){
			state = null;
			stateType = null;
			if (!TryGetTarget(storeId, out IStoreDiagnosticTarget target)) return false;

			state = target.GetState();
			stateType = target.StateType;
			return true;
		}

		public static bool TryReplaceState(long storeId, object state, out string error){
			if (!TryGetTarget(storeId, out IStoreDiagnosticTarget target)) {
				error = $"Store #{storeId} is no longer available.";
				return false;
			}

			return target.TryReplaceState(state, out error);
		}

		internal static void Publish(StoreDiagnosticEvent diagnosticEvent){
			Action<StoreDiagnosticEvent> handlers = EventPublished;
			if (handlers == null) return;

			foreach (Action<StoreDiagnosticEvent> handler in handlers.GetInvocationList()) {
				try {
					handler(diagnosticEvent);
				} catch {
					// Инструмент диагностики не должен менять поведение игрового Dispatch.
				}
			}
		}

		internal static void PublishSubscription(StoreSubscriptionDiagnosticEvent diagnosticEvent){
			Action<StoreSubscriptionDiagnosticEvent> handlers = SubscriptionEventPublished;
			if (handlers == null) return;

			foreach (Action<StoreSubscriptionDiagnosticEvent> handler in handlers.GetInvocationList()) {
				try {
					handler(diagnosticEvent);
				} catch {
					// Диагностика не должна вмешиваться в selector или listener игрового Store.
				}
			}
		}

		internal static void PublishDispatchCompleted(StoreDispatchDiagnosticEvent diagnosticEvent){
			Action<StoreDispatchDiagnosticEvent> handlers = DispatchCompleted;
			if (handlers == null) return;

			foreach (Action<StoreDispatchDiagnosticEvent> handler in handlers.GetInvocationList()) {
				try {
					handler(diagnosticEvent);
				} catch {
					// Финальная метрика Dispatch не должна менять поведение Store.
				}
			}
		}

		private static bool TryGetTarget(long storeId, out IStoreDiagnosticTarget target){
			lock (TargetsLock) {
				if (Targets.TryGetValue(storeId, out WeakReference<IStoreDiagnosticTarget> reference) &&
				    reference.TryGetTarget(out target)) return true;

				Targets.Remove(storeId);
				target = null;
				return false;
			}
		}
	}
}
