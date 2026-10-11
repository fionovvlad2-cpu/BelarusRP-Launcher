using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BelarusRP.Launcher;
using Xunit;

namespace BelarusRP.Tests
{
    // ---------- Юнит-тесты ----------
    public class UnitTests
    {
        [Fact]
        public void Dpapi_RoundTrip()
        {
            string enc = Secure.Protect("secret-token");
            Assert.NotEqual("secret-token", enc);
            Assert.Equal("secret-token", Secure.Unprotect(enc));
        }

        [Fact]
        public void Dpapi_GarbageReturnsEmpty()
        {
            Assert.Equal("", Secure.Unprotect("not-base64!!"));
            Assert.Equal("", Secure.Unprotect(""));
        }

        [Fact]
        public void Sha256_KnownValue()
        {
            string f = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(f, Encoding.ASCII.GetBytes("abc"));
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Secure.Sha256File(f));
            }
            finally { File.Delete(f); }
        }

        [Fact]
        public void BlockedProcesses_NullAndUnknown()
        {
            Assert.Empty(Secure.FindBlockedProcesses(null));
            Assert.Empty(Secure.FindBlockedProcesses(new[] { "definitely-not-a-process-xyz" }));
        }

        [Fact]
        public void Stats_HistoryIsCapped()
        {
            var s = new Stats();
            for (int i = 0; i < 80; i++) s.AddHistory("srv" + i);
            Assert.Equal(50, s.History.Count);
            Assert.Equal("srv79", s.History.Last().Server);
        }

        [Fact]
        public void Stats_PlaytimeFormat()
        {
            Assert.Equal("1:01", new Stats { PlaySeconds = 3660 }.PlaytimeText());
        }

        [Fact]
        public void Loc_UnknownKeyReturnsKey()
        {
            Assert.Equal("NoSuchKey", Loc.Get("NoSuchKey"));
        }
    }

    // ---------- Интеграционные тесты (файлы + манифест) ----------
    public class IntegrationTests : IDisposable
    {
        readonly string _dir = Path.Combine(Path.GetTempPath(), "brp_" + Guid.NewGuid().ToString("N"));
        public IntegrationTests() { Directory.CreateDirectory(_dir); }
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        [Fact]
        public async Task Manifest_AllGood_NoIssues()
        {
            File.WriteAllText(Path.Combine(_dir, "a.txt"), "hello");
            string h = Secure.Sha256File(Path.Combine(_dir, "a.txt"));
            var issues = await Secure.VerifyManifestJsonAsync("{\"files\":{\"a.txt\":\"" + h + "\"}}", _dir);
            Assert.Empty(issues);
        }

        [Fact]
        public async Task Manifest_DetectsModifiedAndMissing()
        {
            File.WriteAllText(Path.Combine(_dir, "a.txt"), "hello");
            var issues = await Secure.VerifyManifestJsonAsync("{\"files\":{\"a.txt\":\"00\",\"b.txt\":\"00\"}}", _dir);
            Assert.Contains("modified: a.txt", issues);
            Assert.Contains("missing: b.txt", issues);
        }
    }

    // ---------- Тесты безопасности ----------
    public class SecurityTests : IDisposable
    {
        readonly string _dir = Path.Combine(Path.GetTempPath(), "brp_sec_" + Guid.NewGuid().ToString("N"));
        public SecurityTests() { Directory.CreateDirectory(_dir); }
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        [Theory]
        [InlineData("..\\..\\Windows\\win.ini")]
        [InlineData("../../etc/passwd")]
        [InlineData("..\\outside.txt")]
        public async Task Manifest_PathTraversal_Rejected(string evil)
        {
            string json = "{\"files\":{" + System.Text.Json.JsonSerializer.Serialize(evil) + ":\"00\"}}";
            var issues = await Secure.VerifyManifestJsonAsync(json, _dir);
            Assert.Contains(issues, i => i.StartsWith("bad path"));
        }

        [Fact]
        public async Task Manifest_RejectsPlainHttp()
        {
            var issues = await Secure.VerifyManifestAsync("http://example.com/m.json", _dir);
            Assert.Single(issues);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{\"nofiles\":1}")]
        public async Task Manifest_BadJson_ReportsIssueNotCrash(string json)
        {
            var issues = await Secure.VerifyManifestJsonAsync(json, _dir);
            Assert.NotEmpty(issues);
        }
    }

    // ---------- Стресс-тесты ----------
    public class StressTests : IDisposable
    {
        readonly string _dir = Path.Combine(Path.GetTempPath(), "brp_stress_" + Guid.NewGuid().ToString("N"));
        public StressTests() { Directory.CreateDirectory(_dir); }
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        [Fact]
        public async Task Manifest_2000Files()
        {
            var sb = new StringBuilder("{\"files\":{");
            for (int i = 0; i < 2000; i++)
            {
                string name = "f" + i + ".bin";
                File.WriteAllText(Path.Combine(_dir, name), "data" + i);
                if (i > 0) sb.Append(',');
                sb.Append("\"" + name + "\":\"" + Secure.Sha256File(Path.Combine(_dir, name)) + "\"");
            }
            sb.Append("}}");
            var issues = await Secure.VerifyManifestJsonAsync(sb.ToString(), _dir);
            Assert.Empty(issues);
        }

        [Fact]
        public void Dpapi_ParallelCalls()
        {
            Parallel.For(0, 500, i => Assert.Equal("v" + i, Secure.Unprotect(Secure.Protect("v" + i))));
        }

        [Fact]
        public void Hash_LargeFile_50MB()
        {
            string f = Path.Combine(_dir, "big.bin");
            using (var fs = File.Create(f)) { var buf = new byte[1 << 20]; for (int i = 0; i < 50; i++) fs.Write(buf, 0, buf.Length); }
            Assert.Equal(64, Secure.Sha256File(f).Length);
        }
    }
}
