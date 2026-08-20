# NetworkObject Spawn / Despawn Activity

This Unity 6 project demonstrates server-authoritative spawning, synchronized behavior, and despawning with Netcode for GameObjects.

## Included networking features

| Requirement | Project implementation |
|---|---|
| NetworkManager | `NetworkManager` in `NetworkSpawnDemo.unity` starts Host or Client. |
| Unity Transport | `UnityTransport` uses address `127.0.0.1` and port `7777` by default. |
| NetworkObject | The controller and spawned cube both use `NetworkObject`. |
| Network Prefabs | `NetworkCube.prefab` is registered in `NetworkPrefabsList.asset`. |
| NetworkBehaviour | `NetworkSpawnController` and `SpawnedNetworkCube` derive from `NetworkBehaviour`. |
| Instantiate | The server instantiates a fresh `NetworkCube` prefab. |
| Configure | The server sets its sequence, lifetime, tint, transform, and origin. |
| Spawn | The server calls `NetworkObject.Spawn()`, creating a matching clone on every client. |
| Despawn | The server calls `NetworkObject.Despawn(true)` after 10 seconds or through the manual button. |
| Synchronization | `NetworkTransform` copies server motion; `NetworkVariable` values copy ID, tint, and remaining lifetime. |

## Run the demonstration

1. Open this folder with Unity `6000.3.9f1`.
2. Open `Assets/Scenes/NetworkSpawnDemo.unity`.
3. Use **File > Build and Run** to make one standalone instance.
4. Keep the Unity Editor in Play Mode as the second instance.
5. In one instance, select **Start Host**.
6. In the other, keep `127.0.0.1` and port `7777`, then select **Start Client**.
7. Press **Spawn Network Cube** in either instance.
8. Verify that the same Network Object ID, color, motion, and countdown appear in both.
9. Wait 10 seconds and verify that the cube disappears from both, or press **Despawn All Cubes**.

The client button does not instantiate a local GameObject. It invokes an RPC on the in-scene `NetworkSpawnController`; the server performs the authoritative spawn or despawn.

## Suggested evidence for submission

- Screenshot 1: Host and Client both showing the same cube and Network Object ID.
- Screenshot 2: Hierarchy/Inspector showing `NetworkManager` plus `UnityTransport`.
- Screenshot 3: `NetworkCube.prefab` showing `NetworkObject`, `NetworkTransform`, and `SpawnedNetworkCube`.
- Short video: press Spawn on the client, show the cube appear in both, then wait for synchronized despawn.

## Rebuild generated assets

The scene, prefab, material, and prefab-list asset can be recreated from source using:

`Tools > Multiplayer Activity > Rebuild Demo Scene`

To make a Windows executable, use:

`Tools > Multiplayer Activity > Build Windows Player`
