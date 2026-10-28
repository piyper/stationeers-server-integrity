# Server Integrity

The Integrity Gun is cloned from Stationeers' native Spray Gun prefab. Using it on a world object toggles that object's native `Thing.Indestructable` property. Stationeers serializes that property into `world.xml`, so the state survives saving and restarting.

The item is available in the creative spawn menu and as a Tier Two Tool Manufacturer recipe. The mod must be installed on both the dedicated server and connecting clients.

The Ownership Gun assigns the Steam ID stored as a structure's owner. Label it with an online player's name or a numeric Steam ID. Its selector uses the same target-only and connected-construction modes. It is unrestricted in single-player and restricted to authenticated server administrators in multiplayer.

The Integrity Scanner SPU can be inserted into Sensor Lenses. While the lenses are powered, structures within 50 metres are rendered through walls using the native T-Ray overlay material:

- Red: indestructible and protected from deconstruction.
- Green: destructible and available for normal deconstruction.

## Behaviour

- First use: makes the target indestructible.
- Second use: restores normal damage behaviour.
- Selector Off: changes only the targeted object.
- Selector On: changes the connected construction, following structural neighbours, attached devices, and physically touching construction bounds.
- The gun does not consume paint and does not require a spray can.
- On a server, non-administrators can use the Integrity Gun only on structures they own.
- Single-player treats the local player as an administrator.
- Each gun has a 250 ms activation cooldown to prevent click bounce.
- The target's prefab is unchanged; only its native damage flag changes.
