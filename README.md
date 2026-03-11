# http-libs

[![CI](https://github.com/damianh/http-hybridcache-handler/actions/workflows/ci.yml/badge.svg)](https://github.com/damianh/http-hybridcache-handler/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/damianh/http-hybridcache-handler.svg)](https://github.com/damianh/http-hybridcache-handler/stargazers)

A collection of .NET libraries for HTTP caching, structured field values, and message signatures.

> [!NOTE]
> Projects are new and being dog-fooded. If you try any of them out feedback would be appreciated!

## Packages

| Package | Description | NuGet | Downloads |
|---------|-------------|-------|-----------|
| [DamianH.HttpHybridCacheHandler](src/HttpHybridCacheHandler/README.md) | RFC 9111 client-side HTTP caching handler for `HttpClient` | [![NuGet](https://img.shields.io/nuget/v/DamianH.HttpHybridCacheHandler.svg)](https://www.nuget.org/packages/DamianH.HttpHybridCacheHandler/) | [![Downloads](https://img.shields.io/nuget/dt/DamianH.HttpHybridCacheHandler.svg)](https://www.nuget.org/packages/DamianH.HttpHybridCacheHandler/) |
| [DamianH.FileDistributedCache](src/FileDistributedCache/README.md) | File-based `IDistributedCache` / `IBufferDistributedCache` for zero-infrastructure persistent caching | [![NuGet](https://img.shields.io/nuget/v/DamianH.FileDistributedCache.svg)](https://www.nuget.org/packages/DamianH.FileDistributedCache/) | [![Downloads](https://img.shields.io/nuget/dt/DamianH.FileDistributedCache.svg)](https://www.nuget.org/packages/DamianH.FileDistributedCache/) |
| [DamianH.Http.StructuredFieldValues](src/Http.StructuredFieldValues/README.md) | RFC 8941/9651 parser, serializer, and POCO mapper for HTTP Structured Field Values | | |
| [DamianH.Http.HttpSignatures](src/Http.HttpSignatures/README.md) | RFC 9421 HTTP Message Signatures for signing and verifying HTTP messages | | |

## Repository Structure

```
src/
  HttpHybridCacheHandler/     # RFC 9111 HTTP caching DelegatingHandler
  FileDistributedCache/       # File-based IDistributedCache implementation
  Http.StructuredFieldValues/ # RFC 8941/9651 structured field values
  Http.HttpSignatures/        # RFC 9421 HTTP message signatures
test/                         # Integration and unit tests
samples/                      # Sample projects per library
benchmarks/                   # Memory allocation benchmarks
```

## Building

```bash
dotnet build http-lib.slnx
```

## Running Tests

```bash
dotnet test http-lib.slnx
```

## License

MIT — see [LICENSE](LICENSE).

## Contributing

Bug reports should be accompanied by a reproducible test case in a pull request.
