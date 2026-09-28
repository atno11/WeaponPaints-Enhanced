

# WeaponPaints-Enhanced

An independent, long-term fork of
[cs2-WeaponPaints](https://github.com/Nereziel/cs2-WeaponPaints),
focused on maintainability, stability and extensibility for
Counter-Strike 2 servers.

> **Project Status:** Early development. The initial release will
> focus on the standalone CS2 plugin, without a required website
> or external API.

## About This Project

WeaponPaints-Enhanced is an independent project based on the
original cs2-WeaponPaints plugin created by Nereziel.

Our goal is to build a maintainable foundation for long-term
development while preserving compatibility with existing
CounterStrikeSharp installations.

The project will evolve independently, with gradual refactoring,
bug fixes and new features.

## Compatibility

The plugin installation directory remains:

`addons/counterstrikesharp/plugins/WeaponPaints`

This path is intentionally preserved to maintain compatibility
with existing server installations.

Changes to configuration formats or other compatibility-sensitive
behavior will be documented as development progresses.

## Credits

WeaponPaints-Enhanced is based on the original
[cs2-WeaponPaints](https://github.com/Nereziel/cs2-WeaponPaints)
project by Nereziel.

All applicable original copyright notices and license
requirements are retained.

This is an independent fork and is not an official release
of the original project.

## Project Status

**Current phase: MVP development**

The initial release focuses on the CS2 plugin itself.

Our priorities are:

- Preserve and validate existing plugin functionality.
- Remove the bundled PHP website dependency.
- Fix critical bugs and improve reliability.
- Maintain compatibility with CounterStrikeSharp.
- Establish a foundation for future development.

Existing functionality is being reviewed and tested.
Features are not considered stable until validated.

## Description

WeaponPaints-Enhanced allows CS2 server operators to provide
cosmetic customization through a CounterStrikeSharp plugin.

Player selections are persisted in a MySQL database and
synchronized by the game server.

The plugin is designed to operate independently of any
website, frontend framework or external API.

A dedicated API is planned for future releases, allowing
developers to build their own frontend using their preferred
languages and frameworks.

## MVP Scope

The first release focuses on the existing game-server
functionality.

Core priorities:

- Weapon skin customization.
- Knife selection and customization.
- Paint, seed and wear configuration.
- MySQL persistence.
- Player data synchronization.
- In-game commands and menus.
- Configuration and installation without a website.

Additional cosmetic features inherited from the original
project will be reviewed and retained as compatibility permits.

### Out of Scope

The following are not part of the initial MVP:

- Bundled PHP website.
- Web-based Steam authentication.
- Public REST API.
- Official web frontend.
- External account-management services.

The initial release does not require PHP or a web server.

## Features

### Core Plugin

- Weapon and knife customization.
- Configurable skin properties.
- Persistent MySQL storage.
- Automatic synchronization when players connect.
- Manual skin refresh with a configurable cooldown.
- In-game customization menus.
- Localization support inherited from the original project.

### Additional Cosmetic Features

The original plugin includes support for gloves, agents,
pins and music kits.

These features will be reviewed and tested as part of the
fork's compatibility and stabilization work.

### Future API

A dedicated API is planned for a later development phase.

The API will provide a secure integration layer between
the plugin's data and external applications.

Frontend developers will be free to choose their own
technology, without being tied to PHP or any particular
JavaScript framework.

The API's implementation language and public contract
have not yet been finalized.

## Requirements

The initial fork is based on the original plugin's
requirements:

- Counter-Strike 2 Dedicated Server.
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
  with runtime support.
- MySQL database.
- [MenuManagerCS2](https://github.com/NickFox007/MenuManagerCS2).
- [PlayerSettings](https://github.com/NickFox007/PlayerSettingsCS2).
- [AnyBaseLibCS2](https://github.com/NickFox007/AnyBaseLibCS2).

Inherited dependencies will be reviewed as the project
is refactored.

PHP, a web server and a Steam Web API key are not required
for the standalone MVP.

## Installation

Installation instructions will be finalized after the
initial Enhanced build is validated.

The intended installation process is:

1. Install CounterStrikeSharp and the required dependencies.
2. Build WeaponPaints-Enhanced or obtain an available release.
3. Install the plugin in the existing `WeaponPaints` directory.
4. Install the required gamedata file, if supplied.
5. Start the server to generate the plugin configuration.
6. Configure the MySQL connection.
7. Restart the server and verify that the plugin loads.

The existing configuration directory is:

`addons/counterstrikesharp/configs/plugins/WeaponPaints/`

The database credentials are configured in
`WeaponPaints.json`.

## Roadmap

### Phase 1 — Standalone MVP

- [ ] Validate existing plugin functionality.
- [ ] Remove the bundled PHP website.
- [ ] Remove or disable website-only functionality.
- [ ] Verify MySQL persistence and player synchronization.
- [ ] Test in-game commands and cosmetic customization.
- [ ] Publish a validated initial release.

### Phase 2 — Stabilization

- [ ] Fix confirmed bugs.
- [ ] Improve error handling and diagnostics.
- [ ] Refactor the existing codebase.
- [ ] Review inherited dependencies.
- [ ] Improve installation and configuration documentation.

### Phase 3 — API

- [ ] Design a versioned API contract.
- [ ] Implement secure access to player cosmetic data.
- [ ] Support authentication and authorization.
- [ ] Document endpoints for third-party developers.
- [ ] Keep API integration optional for game servers.

### Phase 4 — Extended Development

- [ ] Expand cosmetic customization capabilities.
- [ ] Improve extensibility and integration options.
- [ ] Support community-driven improvements.

The roadmap may change as the project evolves.

## Community

Bug reports, feature suggestions and contributions
are welcome through GitHub Issues and Pull Requests.

Please include relevant logs and reproduction steps
when reporting a bug.

## License

WeaponPaints-Enhanced is a derivative of cs2-WeaponPaints
and retains the applicable GNU General Public License
version 3 requirements.

See [LICENSE](LICENSE) for details.

## Disclaimer

Cosmetic modification plugins may conflict with
Valve's game-server policies.

Server operators are responsible for reviewing the
applicable guidelines and understanding the risks,
including potential Game Server Login Token restrictions.

Use this software at your own risk.
