using System;

namespace Evg.Store {
	public sealed class StoreDiagnosticEvent {
		public long StoreId { get; }
		public string StoreName { get; }
		public Type StateType { get; }
		public StoreOperation Operation { get; }
		public long Sequence { get; }
		public long ParentStoreId { get; }
		public long ParentSequence { get; }
		public int DispatchDepth { get; }
		public DateTimeOffset TimestampUtc { get; }
		public IAction Action { get; }
		public object PreviousState { get; }
		public object CurrentState { get; }
		public bool StateChanged { get; }
		public TimeSpan Duration { get; }
		public Exception Error { get; }

		internal StoreDiagnosticEvent(
			long storeId,
			string storeName,
			Type stateType,
			StoreOperation operation,
			long sequence,
			long parentStoreId,
			long parentSequence,
			int dispatchDepth,
			DateTimeOffset timestampUtc,
			IAction action,
			object previousState,
			object currentState,
			bool stateChanged,
			TimeSpan duration,
			Exception error
		){
			StoreId = storeId;
			StoreName = storeName;
			StateType = stateType;
			Operation = operation;
			Sequence = sequence;
			ParentStoreId = parentStoreId;
			ParentSequence = parentSequence;
			DispatchDepth = dispatchDepth;
			TimestampUtc = timestampUtc;
			Action = action;
			PreviousState = previousState;
			CurrentState = currentState;
			StateChanged = stateChanged;
			Duration = duration;
			Error = error;
		}
	}
}
