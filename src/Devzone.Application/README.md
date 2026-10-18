# Application layer

This project defines use case contracts and application DTOs. Implementations belong in Infrastructure; desktop views should call application services rather than persistence directly. Authorization must be checked in service implementations for each operation, not only by hiding UI controls.

The initial contracts cover company setup, authentication, permission checks, and append-only audit recording. They intentionally do not claim that those workflows are implemented until Infrastructure and a desktop host are added.
