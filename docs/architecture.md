# Architecture

## Projects

- **OmniDrive.Core**: provider-independent models and contracts. Must not depend on UI frameworks or provider SDKs.
- **OmniDrive.Application**: orchestration and shared use cases. Depends on Core, not on concrete providers.
- **OmniDrive.Providers.Telegram**: Telegram-specific authentication, listing, search and transfer implementation. Provider SDK types stay inside this project.
- **OmniDrive.App**: Avalonia desktop shell. It should discover operations from provider/item capabilities rather than hard-code Telegram behavior.
- **OmniDrive.Core.Tests / OmniDrive.Application.Tests**: fast tests that do not require live accounts or network access.

## Provider contract

Implementations of `IStorageProvider` declare account-level capabilities and return item-level capabilities on each `StorageItem`. The UI must intersect these capabilities before presenting an operation. Providers may reject operations that are unavailable for a particular item; the application should surface that as a clear, actionable error.

## Telegram integration notes

Telegram is a messaging platform, not a conventional filesystem. The integration should model eligible messages and media as items and expose only operations that the Telegram API and WTelegramClient can actually perform. A Telegram message history must not be presented as a real folder tree unless the provider can faithfully map it.

Credentials, API hashes, phone numbers, authentication codes and session files must never be committed. Prefer environment/configuration inputs for non-secret settings and OS-backed secure storage for session secrets. Local session files, if used by the library, must remain outside source control.

## Transfers

Cross-provider transfer is an application use case: download to a controlled temporary stream/file, then upload to the destination provider. It should report progress, support cancellation where possible, clean up temporary files, and avoid buffering large files entirely in memory.

## Initial delivery sequence

1. Establish solution and contracts.
2. Add desktop shell.
3. Implement Telegram connection and account state.
4. List eligible Telegram messages/media.
5. Download a selected item with progress and cancellation.
6. Add upload and cross-provider transfer after verifying API capabilities and constraints.
