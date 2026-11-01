using System;
using System.Collections.Generic;

namespace Evg.Store {
	public interface IStoreService<TState> {
		void SetStore(TState newState);
		TState GetStore();
		void Dispatch(IAction action);
		IDisposable Subscribe(Action<TState> listener);
		IDisposable Subscribe<TSelected>(
			Func<TState, TSelected> selector,
			Action<TSelected> listener,
			IEqualityComparer<TSelected> comparer = null
		);
	}
}
