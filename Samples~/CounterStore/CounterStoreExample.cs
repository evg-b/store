using System;
using Evg.Store;
using UnityEngine;

namespace Evg.Store.Samples.CounterStore {
	// Компонент владеет локальным Store и показывает изменение выбранного значения в Console.
	public sealed class CounterStoreExample : MonoBehaviour {
		private IStoreService<CounterState> _store;
		private IDisposable _countSubscription;

		private void Awake(){
			_store = new StoreService<CounterState>(new CounterState(Count: 0), CounterReducer.Reduce);
			// Subscribe сразу вызывает OnCountChanged, поэтому Store уже должен быть создан.
			_countSubscription = _store.Subscribe(state => state.Count, OnCountChanged);
		}

		// Метод назначается в On Click кнопки; Action хранит только данные для редьюсера.
		public void Increment(){
			_store.Dispatch(new IncrementCounterAction(Amount: 1));
		}

		private void OnCountChanged(int count){
			Debug.Log($"Counter State changed: {count}", this);
		}

		private void OnDestroy(){
			// Store удерживает обработчик, поэтому освобождаем подписку вместе с объектом.
			_countSubscription?.Dispose();
		}
	}

	// Record обеспечивает равенство по Count и обновление состояния через with.
	public sealed record CounterState(int Count);

	public sealed record IncrementCounterAction(int Amount) : IAction;

	// Чистый редьюсер не обращается к сцене и сохраняет прежние экземпляры State.
	public static class CounterReducer {
		public static CounterState Reduce(CounterState state, IAction action){
			if (action is not IncrementCounterAction increment || increment.Amount == 0) {
				return state;
			}

			return state with { Count = state.Count + increment.Amount };
		}
	}
}
