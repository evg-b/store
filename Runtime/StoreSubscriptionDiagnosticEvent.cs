using System;

namespace Evg.Store {
	public sealed class StoreSubscriptionDiagnosticEvent {
		public long StoreId { get; }
		public long SubscriptionId { get; }
		public string StoreName { get; }
		public Type StateType { get; }
		public StoreSubscriptionKind Kind { get; }
		public StoreSubscriptionOperation Operation { get; }
		public long DispatchSequence { get; }
		public int DispatchDepth { get; }
		public DateTimeOffset TimestampUtc { get; }
		public string SelectorName { get; }
		public string ListenerName { get; }
		public string ListenerTargetType { get; }
		public string ComparerName { get; }
		public WeakReference ListenerTarget { get; }
		public string SourceFilePath { get; }
		public int SourceLineNumber { get; }
		public TimeSpan SelectorDuration { get; }
		public TimeSpan ListenerDuration { get; }
		public bool SelectedValueChanged { get; }
		public bool ListenerNotified { get; }
		public Exception Error { get; }

		internal StoreSubscriptionDiagnosticEvent(
			long storeId,
			long subscriptionId,
			string storeName,
			Type stateType,
			StoreSubscriptionKind kind,
			StoreSubscriptionOperation operation,
			long dispatchSequence,
			int dispatchDepth,
			DateTimeOffset timestampUtc,
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
			Exception error
		){
			StoreId = storeId;
			SubscriptionId = subscriptionId;
			StoreName = storeName;
			StateType = stateType;
			Kind = kind;
			Operation = operation;
			DispatchSequence = dispatchSequence;
			DispatchDepth = dispatchDepth;
			TimestampUtc = timestampUtc;
			SelectorName = selectorName;
			ListenerName = listenerName;
			ListenerTargetType = listenerTargetType;
			ComparerName = comparerName;
			ListenerTarget = listenerTarget == null ? null : new WeakReference(listenerTarget);
			SourceFilePath = sourceFilePath;
			SourceLineNumber = sourceLineNumber;
			SelectorDuration = selectorDuration;
			ListenerDuration = listenerDuration;
			SelectedValueChanged = selectedValueChanged;
			ListenerNotified = listenerNotified;
			Error = error;
		}
	}
}
