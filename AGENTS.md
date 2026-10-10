# Repository conventions

- Release versions use `yy.mm.dd.HH`, using the maintainer's local date and hour (America/New_York). Keep NuGet, assembly, and file versions consistent. Numeric components may omit leading zeros.
- Preserve the .NET Framework 4.8, .NET 8, and .NET 10 library targets and the existing wire schema unless a protocol change is explicitly requested.
- NetMQ sockets must be created, used, and disposed on their owning thread. Do not call process-wide `NetMQConfig.Cleanup` from instance disposal.
- Build and run transport regression tests before merging lifecycle changes.
