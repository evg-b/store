# Counter Store sample

[Store overview](../../README.md) · [Обзор на русском](../../README.ru.md)

The sample uses `sealed record` for `CounterState` and `IncrementCounterAction`, a pure reducer with `with`, and a selector subscription owned by a Unity component.

## Before importing

Enable C# 9 record support in the assembly that will contain the imported scripts. Follow the [IsExternalInit instructions](../../README.md#enable-records-in-your-assembly) before importing the sample.

The sample has no assembly definition, so Unity normally imports it into `Assembly-CSharp`. If you move it under a custom `.asmdef`, that assembly must reference `Evg.Store` and have access to `IsExternalInit`. Reuse your existing compatibility file when available.

## Run in Unity

1. In **Window > Package Manager**, select **Store** and import **Counter Store** from **Samples**.
2. Open a scene and add `CounterStoreExample` to an active GameObject. If using a new empty test scene, include an enabled Camera targeting Display 1.
3. Add a UI Button and connect its **On Click** event to `CounterStoreExample.Increment()` on that GameObject. Ensure the scene has the Canvas and EventSystem required by your UI setup.
4. Enter Play Mode with the Console open. The immediate subscription callback prints `Counter State changed: 0`.
5. Press the button twice. The Console should show `Counter State changed: 1`, then `Counter State changed: 2`.
6. Destroy the sample GameObject or leave Play Mode. `OnDestroy` disposes the subscription.

`Increment()` is a public method for a Button or another script to call. The component does not add an Inspector button of its own.

`Awake` creates the store before subscribing. A zero increment or an unrelated action returns the previous state, so it does not notify the listener. The subscription is active while the component's GameObject exists, including while the component is disabled.

These steps describe the expected interactive behavior. See the [validation report](../../Documentation~/validation.md) for which checks were actually run.
