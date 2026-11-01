using System;

namespace Evg.Store {
	public enum StoreDispatchFailurePhase {
		None,
		Reducer,
		Selector,
		Listener
	}

	public sealed class StoreDispatchDiagnosticEvent {
		public long StoreId { get; }
		public string StoreName { get; }
		public Type StateType { get; }
		public long Sequence { get; }
		public long ParentStoreId { get; }
		public long ParentSequence { get; }
		public int Depth { get; }
		public DateTimeOffset TimestampUtc { get; }
		public IAction Action { get; }
		public TimeSpan ReducerDuration { get; }
		public TimeSpan SelectorDuration { get; }
		public TimeSpan ListenerDuration { get; }
		public TimeSpan OwnDuration { get; }
		public TimeSpan NestedDispatchDuration { get; }
		public TimeSpan TotalDuration { get; }
		public int SubscriptionEvaluations { get; }
		public int SubscriptionNotifications { get; }
		public int ComparerSkips { get; }
		public StoreDispatchFailurePhase FailurePhase { get; }
		public Exception Error { get; }

		internal StoreDispatchDiagnosticEvent(
			long storeId,
			string storeName,
			Type stateType,
			long sequence,
			long parentStoreId,
			long parentSequence,
			int depth,
			DateTimeOffset timestampUtc,
			IAction action,
			TimeSpan reducerDuration,
			TimeSpan selectorDuration,
			TimeSpan listenerDuration,
			TimeSpan ownDuration,
			TimeSpan nestedDispatchDuration,
			TimeSpan totalDuration,
			int subscriptionEvaluations,
			int subscriptionNotifications,
			int comparerSkips,
			StoreDispatchFailurePhase failurePhase,
			Exception error
		){
			StoreId = storeId;
			StoreName = storeName;
			StateType = stateType;
			Sequence = sequence;
			ParentStoreId = parentStoreId;
			ParentSequence = parentSequence;
			Depth = depth;
			TimestampUtc = timestampUtc;
			Action = action;
			ReducerDuration = reducerDuration;
			SelectorDuration = selectorDuration;
			ListenerDuration = listenerDuration;
			OwnDuration = ownDuration;
			NestedDispatchDuration = nestedDispatchDuration;
			TotalDuration = totalDuration;
			SubscriptionEvaluations = subscriptionEvaluations;
			SubscriptionNotifications = subscriptionNotifications;
			ComparerSkips = comparerSkips;
			FailurePhase = failurePhase;
			Error = error;
		}
	}

	internal sealed class StoreDispatchMeasurement {
		public long StoreId;
		public long Sequence;
		public int Depth;
		public long WallStartedAt;
		public IAction Action;
		public StoreDispatchMeasurement Parent;
		public bool CapturePipeline;
		public long ReducerTicks;
		public long SelectorTicks;
		public long ListenerTicks;
		public long NestedDispatchTicks;
		public long NestedWallTicks;
		public int SubscriptionEvaluations;
		public int SubscriptionNotifications;
		public int ComparerSkips;
		public StoreDispatchFailurePhase FailurePhase;
		public Exception Error;
	}
}
