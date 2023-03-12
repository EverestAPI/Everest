using MonoMod;
using System.Collections.Concurrent;
using System.Threading;

namespace NETCoreifier {
    // No code shouldn't be using Thread.Abort anyway, but some does ._.
    // So we at least have to try to restore its functionality
    public static class ThreadAbortShims {

        // A semi-jank way of relinking ThreadAbortException to ThreadInterruptedException
        [MonoModPatch("System.Threading.ThreadInterruptedException")]
        [MonoModLinkFrom("System.Threading.ThreadAbortException")]
        public class ThreadAbortException : ThreadInterruptedException {}

        private static ConcurrentDictionary<int, object> ThreadAbortStates = new ConcurrentDictionary<int, object>();

        // Wrap thread constructors to handle """ThreadAbortException"""s

        [MonoModLinkFrom("System.Void System.Threading.Thread::.ctor(System.Threading.ThreadStart)")]
        public static Thread Thread_ctor(ThreadStart start) => Thread_ctor(start, 0);
        [MonoModLinkFrom("System.Void System.Threading.Thread::.ctor(System.Threading.ThreadStart,System.Int32)")]
        public static Thread Thread_ctor(ThreadStart start, int maxStackSize) => new Thread(() => {
            try {
                start();
            } catch (ThreadInterruptedException) {
                // If this is a ThreadAbortException, there will be an entry in ThreadAbortStates we can remove
                if (!ThreadAbortStates.TryRemove(Thread.CurrentThread.ManagedThreadId, out _))
                    throw;
            } finally {
                ThreadAbortStates.TryRemove(Thread.CurrentThread.ManagedThreadId, out _);
            }
        }, maxStackSize);

        [MonoModLinkFrom("System.Void System.Threading.Thread::.ctor(System.Threading.ParameterizedThreadStart)")]
        public static Thread Thread_ctor(ParameterizedThreadStart start) => Thread_ctor(start, 0);
        [MonoModLinkFrom("System.Void System.Threading.Thread::.ctor(System.Threading.ParameterizedThreadStart,System.Int32)")]
        public static Thread Thread_ctor(ParameterizedThreadStart start, int maxStackSize) => new Thread(arg => {
            try {
                start(arg);
            } catch (ThreadInterruptedException) {
                // If this is a ThreadAbortException, there will be an entry in ThreadAbortStates we can remove
                if (!ThreadAbortStates.TryRemove(Thread.CurrentThread.ManagedThreadId, out _))
                    throw;
            } finally {
                ThreadAbortStates.TryRemove(Thread.CurrentThread.ManagedThreadId, out _);
            }
        }, maxStackSize);

        [MonoModLinkFrom("System.Void System.Threading.Thread::Abort()")]
        public static void Abort(Thread thread) => Abort(thread, null);

        [MonoModLinkFrom("System.Void System.Threading.Thread::Abort(System.Object)")]
        public static void Abort(Thread thread, object state) {
            ThreadAbortStates.AddOrUpdate(thread.ManagedThreadId, state, (_, _) => state);
            thread.Interrupt();
        }

        [MonoModLinkFrom("System.Void System.Threading.Thread::ResetAbort()")]
        public static void ResetAbort()
            => ThreadAbortStates.TryRemove(Thread.CurrentThread.ManagedThreadId, out _);

        [MonoModLinkFrom("System.Object System.Threading.ThreadInterruptedException::get_ExceptionState()")]
        public static object get_ExceptionState(ThreadAbortException ex)
            => ThreadAbortStates.TryGetValue(Thread.CurrentThread.ManagedThreadId, out object state) ? state : null;

    }
}