using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Evg.Store.Tests {
	public class StoreServiceTests {
		private sealed class IncrementAction : IAction {
		}

		private static readonly IAction Increment = new IncrementAction();

		[Test]
		public void Subscribe_EmitsCurrentStateAndChangedStates(){
			StoreService<int> store = CreateIncrementingStore();
			List<int> values = new List<int>();

			using (store.Subscribe(values.Add)) {
				store.Dispatch(Increment);
				store.Dispatch(Increment);
			}

			CollectionAssert.AreEqual(new[] { 0, 1, 2 }, values);
		}

		[Test]
		public void SelectorSubscribe_SkipsUnchangedSelectedValues(){
			StoreService<int> store = CreateIncrementingStore();
			List<int> values = new List<int>();

			using (store.Subscribe(state => state / 2, values.Add)) {
				store.Dispatch(Increment);
				store.Dispatch(Increment);
			}

			CollectionAssert.AreEqual(new[] { 0, 1 }, values);
		}

		[Test]
		public void SelectorSubscribe_UsesCustomComparer(){
			StoreService<string> store = new StoreService<string>(
				"A",
				(state, _) => state == "A" ? "a" : "A"
			);
			List<string> values = new List<string>();

			using (store.Subscribe(state => state, values.Add, StringComparer.OrdinalIgnoreCase)) {
				store.Dispatch(Increment);
			}

			CollectionAssert.AreEqual(new[] { "A" }, values);
		}

		[Test]
		public void Subscription_DisposeStopsNotifications(){
			StoreService<int> store = CreateIncrementingStore();
			List<int> values = new List<int>();
			IDisposable subscription = store.Subscribe(values.Add);

			subscription.Dispose();
			store.Dispatch(Increment);

			CollectionAssert.AreEqual(new[] { 0 }, values);
		}

		[Test]
		public void SubscriptionDiagnostics_ReportsEvaluationsNotificationsSkipsAndDispose(){
			List<StoreSubscriptionDiagnosticEvent> events = new List<StoreSubscriptionDiagnosticEvent>();
			void Capture(StoreSubscriptionDiagnosticEvent item) => events.Add(item);
			StoreDiagnostics.SubscriptionEventPublished += Capture;

			try {
				StoreService<int> store = CreateIncrementingStore();
				IDisposable subscription = store.Subscribe(state => state / 2, _ => { });
				store.Dispatch(Increment);
				store.Dispatch(Increment);
				subscription.Dispose();

				Assert.AreEqual(4, events.Count);
				Assert.AreEqual(StoreSubscriptionOperation.Created, events[0].Operation);
				Assert.AreEqual(StoreSubscriptionKind.Selector, events[0].Kind);
				Assert.IsTrue(events[0].SelectedValueChanged);
				Assert.IsTrue(events[0].ListenerNotified);
				Assert.AreEqual(StoreSubscriptionOperation.Evaluated, events[1].Operation);
				Assert.IsFalse(events[1].SelectedValueChanged);
				Assert.IsFalse(events[1].ListenerNotified);
				Assert.IsTrue(events[2].SelectedValueChanged);
				Assert.IsTrue(events[2].ListenerNotified);
				Assert.AreEqual(StoreSubscriptionOperation.Disposed, events[3].Operation);
				Assert.AreEqual(events[0].StoreId, events[3].StoreId);
				Assert.AreEqual(events[0].SubscriptionId, events[3].SubscriptionId);
				Assert.IsNotEmpty(events[0].SelectorName);
				Assert.IsNotEmpty(events[0].ListenerName);
				Assert.IsNotEmpty(events[0].ComparerName);
				Assert.IsNotEmpty(events[0].SourceFilePath);
				StringAssert.EndsWith("StoreServiceTests.cs", events[0].SourceFilePath.Replace('\\', '/'));
				Assert.Greater(events[0].SourceLineNumber, 0);
				Assert.AreEqual(events[0].SourceFilePath, events[3].SourceFilePath);
				Assert.AreEqual(events[0].SourceLineNumber, events[3].SourceLineNumber);
				Assert.GreaterOrEqual(events[2].SelectorDuration, TimeSpan.Zero);
				Assert.GreaterOrEqual(events[2].ListenerDuration, TimeSpan.Zero);
				Assert.Greater(events[1].DispatchSequence, 0);
				Assert.AreEqual(events[1].DispatchSequence, events[2].DispatchSequence - 1);
			} finally {
				StoreDiagnostics.SubscriptionEventPublished -= Capture;
			}
		}

		[Test]
		public void SubscriptionDiagnosticsSubscriber_ExceptionDoesNotBreakStore(){
			void ThrowingSubscriber(StoreSubscriptionDiagnosticEvent _) => throw new InvalidOperationException("tool failed");
			StoreDiagnostics.SubscriptionEventPublished += ThrowingSubscriber;

			try {
				StoreService<int> store = CreateIncrementingStore();
				Assert.DoesNotThrow(() => {
					using (store.Subscribe(state => state, _ => { })) store.Dispatch(Increment);
				});
				Assert.AreEqual(1, store.GetStore());
			} finally {
				StoreDiagnostics.SubscriptionEventPublished -= ThrowingSubscriber;
			}
		}

		[Test]
		public void Listener_CanDispatchSynchronously(){
			StoreService<int> store = CreateIncrementingStore();
			List<int> values = new List<int>();

			using (store.Subscribe(value => {
				values.Add(value);
				if (value == 1) store.Dispatch(Increment);
			})) {
				store.Dispatch(Increment);
			}

			CollectionAssert.AreEqual(new[] { 0, 1, 2 }, values);
		}

		[Test]
		public void Listener_CanDisposeDuringNotification(){
			StoreService<int> store = CreateIncrementingStore();
			IDisposable first = null;
			int secondCalls = 0;

			first = store.Subscribe(value => {
				if (value == 1) first.Dispose();
			});
			using (store.Subscribe(_ => secondCalls++)) {
				Assert.DoesNotThrow(() => store.Dispatch(Increment));
			}

			Assert.AreEqual(2, secondCalls);
		}

		[Test]
		public void SetStore_ReplacesStateWithoutNotification(){
			StoreService<int> store = CreateIncrementingStore();
			List<int> values = new List<int>();

			using (store.Subscribe(values.Add)) {
				store.SetStore(10);
			}

			Assert.AreEqual(10, store.GetStore());
			CollectionAssert.AreEqual(new[] { 0 }, values);
		}

		[Test]
		public void Diagnostics_ReportsCreationDispatchAndStateReplacement(){
			List<StoreDiagnosticEvent> events = new List<StoreDiagnosticEvent>();
			void Capture(StoreDiagnosticEvent item) => events.Add(item);
			StoreDiagnostics.EventPublished += Capture;

			try {
				StoreService<int> store = CreateIncrementingStore();
				store.Dispatch(Increment);
				store.SetStore(10);

				Assert.AreEqual(3, events.Count);
				Assert.AreEqual(StoreOperation.Created, events[0].Operation);
				Assert.AreEqual(StoreOperation.Dispatch, events[1].Operation);
				Assert.AreSame(Increment, events[1].Action);
				Assert.AreEqual(0, events[1].PreviousState);
				Assert.AreEqual(1, events[1].CurrentState);
				Assert.IsTrue(events[1].StateChanged);
				Assert.GreaterOrEqual(events[1].Duration, TimeSpan.Zero);
				Assert.AreEqual(StoreOperation.StateReplaced, events[2].Operation);
				Assert.AreEqual(events[0].StoreId, events[2].StoreId);
				Assert.AreEqual(2, events[2].Sequence);
			} finally {
				StoreDiagnostics.EventPublished -= Capture;
			}
		}

		[Test]
		public void Diagnostics_ReportsNoOpAndReducerError(){
			List<StoreDiagnosticEvent> events = new List<StoreDiagnosticEvent>();
			void Capture(StoreDiagnosticEvent item) => events.Add(item);
			StoreDiagnostics.EventPublished += Capture;

			try {
				StoreService<int> noOpStore = new StoreService<int>(3, (state, _) => state);
				noOpStore.Dispatch(Increment);
				InvalidOperationException expected = new InvalidOperationException("broken reducer");
				StoreService<int> failingStore = new StoreService<int>(7, (_, __) => throw expected);

				Assert.Throws<InvalidOperationException>(() => failingStore.Dispatch(Increment));

				StoreDiagnosticEvent noOp = events.Single(item => item.StoreId == events[0].StoreId && item.Operation == StoreOperation.Dispatch);
				Assert.IsFalse(noOp.StateChanged);
				StoreDiagnosticEvent failed = events.Last();
				Assert.AreSame(expected, failed.Error);
				Assert.AreEqual(7, failed.CurrentState);
				Assert.IsFalse(failed.StateChanged);
			} finally {
				StoreDiagnostics.EventPublished -= Capture;
			}
		}

		[Test]
		public void DiagnosticsSubscriber_ExceptionDoesNotBreakDispatch(){
			void ThrowingSubscriber(StoreDiagnosticEvent _) => throw new InvalidOperationException("tool failed");
			void ThrowingCompletionSubscriber(StoreDispatchDiagnosticEvent _) => throw new InvalidOperationException("completion tool failed");
			StoreDiagnostics.EventPublished += ThrowingSubscriber;
			StoreDiagnostics.DispatchCompleted += ThrowingCompletionSubscriber;

			try {
				StoreService<int> store = CreateIncrementingStore();

				Assert.DoesNotThrow(() => store.Dispatch(Increment));
				Assert.AreEqual(1, store.GetStore());
			} finally {
				StoreDiagnostics.EventPublished -= ThrowingSubscriber;
				StoreDiagnostics.DispatchCompleted -= ThrowingCompletionSubscriber;
			}
		}

		[Test]
		public void DispatchDiagnostics_CorrelatesReducerSelectorsAndListeners(){
			List<StoreDispatchDiagnosticEvent> dispatches = new List<StoreDispatchDiagnosticEvent>();
			List<StoreSubscriptionDiagnosticEvent> subscriptions = new List<StoreSubscriptionDiagnosticEvent>();
			void CaptureDispatch(StoreDispatchDiagnosticEvent item) => dispatches.Add(item);
			void CaptureSubscription(StoreSubscriptionDiagnosticEvent item) => subscriptions.Add(item);
			StoreDiagnostics.DispatchCompleted += CaptureDispatch;
			StoreDiagnostics.SubscriptionEventPublished += CaptureSubscription;

			try {
				StoreService<int> store = CreateIncrementingStore();
				using (store.Subscribe(_ => { }))
				using (store.Subscribe(state => state / 2, _ => { })) {
					subscriptions.Clear();
					store.Dispatch(Increment);
				}

				StoreDispatchDiagnosticEvent completed = dispatches.Single();
				Assert.AreEqual(0, completed.Depth);
				Assert.AreEqual(-1, completed.ParentSequence);
				Assert.AreEqual(2, completed.SubscriptionEvaluations);
				Assert.AreEqual(1, completed.SubscriptionNotifications);
				Assert.AreEqual(1, completed.ComparerSkips);
				Assert.AreEqual(StoreDispatchFailurePhase.None, completed.FailurePhase);
				Assert.IsNull(completed.Error);
				Assert.AreEqual(completed.OwnDuration, completed.TotalDuration);
				Assert.GreaterOrEqual(completed.ReducerDuration, TimeSpan.Zero);
				Assert.GreaterOrEqual(completed.SelectorDuration, TimeSpan.Zero);
				Assert.GreaterOrEqual(completed.ListenerDuration, TimeSpan.Zero);
				List<StoreSubscriptionDiagnosticEvent> evaluations = subscriptions
					.Where(item => item.Operation == StoreSubscriptionOperation.Evaluated)
					.ToList();
				Assert.AreEqual(2, evaluations.Count);
				Assert.IsTrue(evaluations.All(item => item.DispatchSequence == completed.Sequence));
				Assert.IsTrue(evaluations.All(item => item.DispatchDepth == completed.Depth));
			} finally {
				StoreDiagnostics.DispatchCompleted -= CaptureDispatch;
				StoreDiagnostics.SubscriptionEventPublished -= CaptureSubscription;
			}
		}

		[Test]
		public void DispatchDiagnostics_PreservesNestedCausalityWithoutDoubleCountingListener(){
			List<StoreDiagnosticEvent> events = new List<StoreDiagnosticEvent>();
			List<StoreDispatchDiagnosticEvent> completions = new List<StoreDispatchDiagnosticEvent>();
			void CaptureEvent(StoreDiagnosticEvent item) => events.Add(item);
			void CaptureCompletion(StoreDispatchDiagnosticEvent item) => completions.Add(item);
			StoreDiagnostics.EventPublished += CaptureEvent;
			StoreDiagnostics.DispatchCompleted += CaptureCompletion;

			try {
				StoreService<int> store = CreateIncrementingStore();
				using (store.Subscribe(value => {
					if (value == 1) store.Dispatch(Increment);
				})) {
					store.Dispatch(Increment);
				}

				List<StoreDiagnosticEvent> actionEvents = events.Where(item => item.Operation == StoreOperation.Dispatch).ToList();
				Assert.AreEqual(2, actionEvents.Count);
				Assert.Less(actionEvents[0].Sequence, actionEvents[1].Sequence);
				StoreDispatchDiagnosticEvent parent = completions.Single(item => item.Depth == 0);
				StoreDispatchDiagnosticEvent nested = completions.Single(item => item.Depth == 1);
				Assert.AreEqual(parent.StoreId, nested.ParentStoreId);
				Assert.AreEqual(parent.Sequence, nested.ParentSequence);
				Assert.AreEqual(parent.OwnDuration + nested.TotalDuration, parent.TotalDuration);
				Assert.AreEqual(nested.TotalDuration, parent.NestedDispatchDuration);
			} finally {
				StoreDiagnostics.EventPublished -= CaptureEvent;
				StoreDiagnostics.DispatchCompleted -= CaptureCompletion;
			}
		}

		[Test]
		public void DispatchDiagnostics_ReportsSelectorFailurePhase(){
			List<StoreDispatchDiagnosticEvent> completions = new List<StoreDispatchDiagnosticEvent>();
			void Capture(StoreDispatchDiagnosticEvent item) => completions.Add(item);
			StoreDiagnostics.DispatchCompleted += Capture;

			try {
				StoreService<int> store = CreateIncrementingStore();
				using (store.Subscribe<int>(
					state => state == 0 ? state : throw new InvalidOperationException("selector failed"),
					_ => { }
				)) {
					Assert.Throws<InvalidOperationException>(() => store.Dispatch(Increment));
				}

				StoreDispatchDiagnosticEvent failed = completions.Single();
				Assert.AreEqual(StoreDispatchFailurePhase.Selector, failed.FailurePhase);
				Assert.IsInstanceOf<InvalidOperationException>(failed.Error);
			} finally {
				StoreDiagnostics.DispatchCompleted -= Capture;
			}
		}

		[Test]
		public void Diagnostics_TimeTravelReplacesStateAndNotifiesSubscribers(){
			List<StoreDiagnosticEvent> events = new List<StoreDiagnosticEvent>();
			void Capture(StoreDiagnosticEvent item) => events.Add(item);
			StoreDiagnostics.EventPublished += Capture;

			try {
				StoreService<int> store = CreateIncrementingStore();
				long storeId = events.Single(item => item.Operation == StoreOperation.Created).StoreId;
				List<int> values = new List<int>();
				List<int> selectedValues = new List<int>();

				using (store.Subscribe(values.Add))
				using (store.Subscribe(state => state / 10, selectedValues.Add)) {
					Assert.IsTrue(StoreDiagnostics.TryGetState(storeId, out object current, out Type stateType));
					Assert.AreEqual(0, current);
					Assert.AreEqual(typeof(int), stateType);
					Assert.IsTrue(StoreDiagnostics.TryReplaceState(storeId, 10, out string error), error);
				}

				Assert.AreEqual(10, store.GetStore());
				CollectionAssert.AreEqual(new[] { 0, 10 }, values);
				CollectionAssert.AreEqual(new[] { 0, 1 }, selectedValues);
				StoreDiagnosticEvent timeTravel = events.Last();
				Assert.AreEqual(StoreOperation.TimeTravel, timeTravel.Operation);
				Assert.AreEqual(storeId, timeTravel.StoreId);
				Assert.AreEqual(0, timeTravel.PreviousState);
				Assert.AreEqual(10, timeTravel.CurrentState);
				Assert.IsTrue(timeTravel.StateChanged);
				Assert.AreEqual(1, timeTravel.Sequence);
			} finally {
				StoreDiagnostics.EventPublished -= Capture;
			}
		}

		[Test]
		public void Diagnostics_TimeTravelRejectsIncompatibleState(){
			List<StoreDiagnosticEvent> events = new List<StoreDiagnosticEvent>();
			void Capture(StoreDiagnosticEvent item) => events.Add(item);
			StoreDiagnostics.EventPublished += Capture;

			try {
				StoreService<int> store = CreateIncrementingStore();
				long storeId = events.Single().StoreId;

				Assert.IsFalse(StoreDiagnostics.TryReplaceState(storeId, "wrong", out string error));

				StringAssert.Contains(typeof(int).FullName, error);
				Assert.AreEqual(0, store.GetStore());
				Assert.AreEqual(1, events.Count);
			} finally {
				StoreDiagnostics.EventPublished -= Capture;
			}
		}

		private static StoreService<int> CreateIncrementingStore(){
			return new StoreService<int>(0, (state, action) => action is IncrementAction ? state + 1 : state);
		}
	}
}
