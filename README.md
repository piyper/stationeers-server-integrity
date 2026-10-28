# Server Integrity

Server Integrity gives multiplayer communities practical tools to protect player builds from unauthorized damage, deconstruction, ownership changes, and destructive console commands.

## Integrity Gun

Aim at a structure and hold the primary action until the progress bar completes. The gun changes protection according to its selected mode. Right-click cycles through the modes:

- **Toggle target** — changes only the object being aimed at.
- **Toggle connected** — changes the target and the construction connected to it.
- **Toggle matching** — changes connected objects of the same type as the target. For example, targeting a window affects matching windows in that connected build without changing its frames.
- **Toggle blast areas** — finds hazardous equipment associated with the connected build and changes the equipment and nearby structures together. This lets administrators temporarily restore normal explosion damage around furnaces or tanks, then protect the area again afterward.

The action popup shows whether the current use will make the selected objects destructible or indestructible. Protected objects cannot be damaged or normally deconstructed. Changes persist when the world is saved and restarted.

On multiplayer servers, administrators may protect any structure. Other players may use the gun only on structures they own. Single-player users have unrestricted access.

## Ownership Gun

The Ownership Gun lets a server administrator assign a structure to a player. Label the gun with the intended online player's name, aim at the structure, and hold the primary action until it completes. Leaving the gun at its default name assigns ownership to the administrator using it.

Use the gun's selector to choose between changing only the target and changing its connected construction. The target popup reports the current owner by name when that owner can be identified. Ownership changes persist with the world save.

The Ownership Gun cannot be used by non-administrators on a multiplayer server. It is unrestricted in single-player.

## Integrity Scanner SPU

Insert the Integrity Scanner SPU into Sensor Lenses and power them on to inspect nearby construction:

- **Red** — protected from damage and deconstruction.
- **Green** — normal damage and deconstruction are enabled.

This provides a quick visual check of a protected build and makes gaps in its protection easier to find.

## Additional server protection

The mod prevents non-administrators from using mutating `thing` console commands such as spawning and deleting objects. This closes an easy route around normal building and ownership rules while leaving non-mutating commands available.

Server Integrity is intended to deter accidental damage and common griefing on community servers. It does not replace access control: hazardous machinery, printers, consoles, and security equipment should still be kept behind secured doors where appropriate.

## Manufacturing and installation

The Integrity Gun, Ownership Gun, and Integrity Scanner SPU are available from the **Security Printer**. Each recipe requires:

- 1 g gold
- 1 energy
- 10 seconds

The tools are also available through the creative spawn menu.

For multiplayer servers using Server Integrity, install the mod on the dedicated server and every connecting client. A client may leave the mod installed when joining servers that do not use it.
