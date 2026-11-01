namespace Evg.Store {
	public delegate TState Reducer<TState>(TState state, IAction action);
}
