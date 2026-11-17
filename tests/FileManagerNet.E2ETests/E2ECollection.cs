using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;

namespace FileManagerNet.E2ETests;

/// <summary>Defines the shared collection that hosts one browser and one test
/// server for the whole end-to-end run.</summary>
[CollectionDefinition("e2e", DisableParallelization = true)]
public sealed class E2ECollection : ICollectionFixture<TestHostFixture> { }
