#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Threading;
using R3;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public static class ExtensionsCompositeDisposable
    {
        public static CancellationToken ToCancellationToken(this CompositeDisposable disposables)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            disposables.Add(Disposable.Create(() => cancellationTokenSource.Cancel()));
            return cancellationTokenSource.Token;
        }
    }
}