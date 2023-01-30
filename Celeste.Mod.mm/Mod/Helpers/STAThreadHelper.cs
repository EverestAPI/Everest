using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class STAThreadHelper : GameComponent {

        public static STAThreadHelper Instance;

        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static readonly HashSet<object> Enqueued = new HashSet<object>();
        private static readonly Dictionary<object, object> EnqueuedWaiting = new Dictionary<object, object>();

        private static Thread Worker;
        private static CancellationTokenSource WaitTokenSource;

        public STAThreadHelper(Game game)
            : base(game) {
            Instance = this;

            UpdateOrder = -500000;

            WaitTokenSource = new CancellationTokenSource();

            Worker = new Thread(WorkerLoop) {
                Name = "Everest STAThread Worker",
                IsBackground = true
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Worker.SetApartmentState(ApartmentState.STA);
            Worker.Start();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Worker = null;
            WaitTokenSource?.Cancel();
            WaitTokenSource?.Dispose();
            WaitTokenSource = null;
        }

        private static bool Poke() {
            if (WaitTokenSource == null)
                return false;
            WaitTokenSource.Cancel();
            WaitTokenSource.Dispose();
            WaitTokenSource = new CancellationTokenSource();
            return true;
        }

        private static void WorkerLoop() {
            while (Worker != null) {
                while (WaitTokenSource == null)
                    continue;
                try {
                    WaitTokenSource.Token.WaitHandle.WaitOne();
                } catch (OperationCanceledException) {
                } catch (ObjectDisposedException) {
                }

                while (Worker != null) {
                    Action nextAction;
                    lock (Queue) {
                        if (Queue.Count == 0)
                            break;
                        nextAction = Queue.Dequeue();
                    }
                    nextAction.Invoke();
                }
            }
        }

        public static void Do(Action a) {
            if (Thread.CurrentThread == Worker) {
                a();
                return;
            }

            lock (Queue) {
                Queue.Enqueue(a);
            }

            Poke();
        }

        public static void Do(object key, Action a) {
            if (Thread.CurrentThread == Worker) {
                a();
                return;
            }

            lock (Queue) {
                if (!Enqueued.Add(key))
                    return;

                Queue.Enqueue(() => {
                    a?.Invoke();
                    lock (Queue) {
                        Enqueued.Remove(key);
                    }
                });
            }

            Poke();
        }

        public static MaybeAwaitable<T> Get<T>(Func<T> f) {
            if (Thread.CurrentThread == Worker) {
                return new MaybeAwaitable<T>(f());
            }

            MaybeAwaitable<T> awaitable;
            lock (Queue) {
                T result = default;
                Task<T> proxy = new Task<T>(() => result);
                awaitable = new MaybeAwaitable<T>(proxy.GetAwaiter());

                Queue.Enqueue(() => {
                    result = f != null ? f.Invoke() : default;
                    proxy.Start();
                });
            }

            Poke();
            return awaitable;
        }

        public static MaybeAwaitable<T> Get<T>(object key, Func<T> f) {
            if (Thread.CurrentThread == Worker) {
                return new MaybeAwaitable<T>(f());
            }

            MaybeAwaitable<T> awaitable;
            lock (Queue) {
                if (!Enqueued.Add(key))
                    return (MaybeAwaitable<T>) EnqueuedWaiting[key];

                T result = default;
                Task<T> proxy = new Task<T>(() => result);
                awaitable = new MaybeAwaitable<T>(proxy.GetAwaiter());
                EnqueuedWaiting[key] = awaitable;

                Queue.Enqueue(() => {
                    result = f != null ? f.Invoke() : default;
                    proxy.Start();
                    lock (Queue) {
                        EnqueuedWaiting.Remove(key);
                        Enqueued.Remove(key);
                    }
                });
            }

            Poke();
            return awaitable;
        }

    }
}
