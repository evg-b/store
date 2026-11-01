# Store API guide

[English overview](../README.md) · [Обзор на русском](../README.ru.md)

## Core API

The runtime assembly is `Evg.Store` and the namespace is `Evg.Store`. It references neither Unity Engine nor an application-specific state model.

| API | Contract |
| --- | --- |
| `IAction` | Marker interface implemented by action records. |
| `Reducer<TState>` | Delegate with the signature `TState Reduce(TState state, IAction action)`. |
| `StoreService<TState>(initialState, reducer)` | Creates a store with an initial state and a required reducer. |
| `IStoreService<TState>` | Interface for reading, replacing, dispatching, and subscribing. |
| `GetStore()` | Returns the current state without copying it. |
| `SetStore(state)` | Replaces state silently; use for hydration before subscribing. |
| `Dispatch(action)` | Calls the reducer once, commits a changed state, then notifies subscriptions. |
| `Subscribe(listener)` | Immediately emits the full current state and then changed state values. |
| `Subscribe(selector, listener, comparer)` | Immediately emits the selected value and then emits changed selections. |

The constructor rejects a null reducer. Dispatch rejects a null action. Subscribe rejects a null selector or listener. State validation belongs to the application.

Use `sealed record` for state and actions. The [README](../README.md#enable-records-in-your-assembly) describes the `IsExternalInit` prerequisite, including placement in the assembly that declares the records.

## Dispatch and equality

Dispatch runs synchronously on its calling thread:

1. Call the reducer with the current state and action.
2. Compare its result to the previous state using `Equals(previous, next)`.
3. If they differ, store the result and notify subscribers.

An equal result does not replace the stored state. Return the original state for an irrelevant action or an unchanged value. Keep reducers pure and selectors free of side effects.

Records compare their properties using each property's equality semantics. A list or dictionary usually compares by reference. A fresh record that contains the same mutable list is not an independent snapshot.

## Collections and records

Copy a collection before changing it, then return a new record. This complete example exposes the copied list through a read-only wrapper:

```csharp
using System;
using System.Collections.Generic;
using Evg.Store;

public sealed record InventoryState(IReadOnlyList<int> ItemIds);
public sealed record AddItemAction(int ItemId) : IAction;

public static class InventoryReducer
{
    public static IStoreService<InventoryState> CreateStore()
    {
        return new StoreService<InventoryState>(
            new InventoryState(ItemIds: Array.Empty<int>()),
            Reduce
        );
    }

    public static InventoryState Reduce(InventoryState state, IAction action)
    {
        if (action is not AddItemAction addItem)
        {
            return state;
        }

        List<int> itemIds = new List<int>(state.ItemIds);
        if (itemIds.Contains(addItem.ItemId))
        {
            return state;
        }

        itemIds.Add(addItem.ItemId);
        return state with { ItemIds = itemIds.AsReadOnly() };
    }
}
```

A selector such as `state => state.ItemIds` sees a new reference when an item is added. Selecting `state => state.ItemIds.Count` limits notifications to changes in count.

A custom `IEqualityComparer<TSelected>` can suppress notifications for distinct snapshots with equal contents. It cannot detect the old contents of a collection that has already been mutated in place. Store does not freeze or deep-copy objects; the application must preserve earlier states.

## Subscription lifecycle

Both Subscribe overloads call the listener **before returning the subscription token**. Set up anything the callback needs before calling Subscribe. If the initial selector or listener throws, Subscribe propagates the error and does not leave an active subscription behind.

A selector is evaluated on changed states. Its listener runs only when the selected value differs according to the comparer, which defaults to `EqualityComparer<TSelected>.Default`. A custom comparer is for the selected value, not the entire store.

Keep each returned `IDisposable` and dispose it when its consumer is destroyed or stops observing. A live store holds ordinary subscription callbacks strongly. The Unity [Counter Store sample](../Samples~/CounterStore/CounterStoreExample.cs) owns its subscription from `Awake` to `OnDestroy`.

Notification uses a snapshot of the listener list. Disposing during a callback is safe, but a callback already captured in that snapshot can still be invoked in the current pass. A newly added subscription receives its immediate value; it is included in subsequent notification snapshots.

## Nested dispatch and threads

A listener may dispatch synchronously. A selector subscription saves its new selected value before invoking the listener, so a nested Dispatch compares against that value. Make the chain terminate; an unconditional dispatch from a callback can recurse indefinitely.

Nested Dispatch completes before the outer callback resumes. Remaining callbacks in the outer notification pass read the store's current state, which may already include nested updates. Dispatch is not a queued transaction.

Store does not synchronize state or subscriptions and does not switch threads. In Unity, call it on the main thread when callbacks access Unity APIs. Marshal external work to the appropriate thread before dispatching.

## Hydration

Use `SetStore` to restore a saved state before creating observers. It assigns the state even if it compares equal and does not notify subscriptions or refresh their cached selections.

Calling it after observers have started can leave consumers displaying old values. Use a normal action and reducer when a replacement must follow the application's notification flow.

Persistence, serialization, and the choice of application state are responsibilities of the consumer.

## Exceptions

- Reducer exceptions propagate. A pure reducer that throws leaves the stored state unchanged.
- Selector, comparer, and listener exceptions propagate. After a changed reducer result has been committed, these errors do not roll it back.
- An exception can interrupt the remaining callbacks in the current notification pass.
- Exceptions in optional diagnostic event handlers are isolated from application dispatch.

Store does not retry failed work or provide transactional rollback.

## Optional runtime diagnostics

Store works without a diagnostic subscriber. Consumers can observe these native events without adding another package:

| API | Data |
| --- | --- |
| `StoreDiagnostics.EventPublished` | Store creation, Dispatch, state replacement, Time Travel, reducer errors, and no-op results. |
| `StoreDiagnostics.SubscriptionEventPublished` | Subscription creation, evaluation, comparer skips, notifications, disposal, callback timing, and errors. |
| `StoreDiagnostics.DispatchCompleted` | Completed dispatch metrics, failure phase, subscription counts, and nested-dispatch correlation. |
| `StoreDiagnostics.TryGetState` | Resolves a registered store's current state by its diagnostic identity. |
| `StoreDiagnostics.TryReplaceState` | Validates the state type, performs a diagnostic replacement, and notifies subscriptions when values change. |

The diagnostic registry holds weak references to stores. Listener targets in emitted subscription events are weak references; this does not change the lifetime of ordinary store subscriptions.

Detailed pipeline timing and best-effort subscription source capture are enabled only while the corresponding diagnostic consumers are attached. A dispatch is correlated by `StoreId + Sequence`. Parent identity and depth track synchronous nesting; own and nested durations avoid counting child work twice. Diagnostic state payloads are object references rather than deep copies.

Detach diagnostic event handlers when their consumer ends its lifetime. `TryReplaceState` is separate from silent `SetStore` and emits a `TimeTravel` event.

## Validation

See the [validation report](validation.md) for the checked source files, commands, results, and Unity-specific limitations. The [release guide](publishing.md) covers extracting this package into a repository with `package.json` at its root.
