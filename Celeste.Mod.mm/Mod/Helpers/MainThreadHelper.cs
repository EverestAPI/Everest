using Microsoft.Xna.Framework;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class MainThreadHelper : GameComponent {

        public static MainThreadHelper Instance;

        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static readonly HashSet<object> Enqueued = new HashSet<object>();
        private static readonly Dictionary<object, object> EnqueuedWaiting = new Dictionary<object, object>();
        public static Thread MainThread { get; private set; }
        public static bool UpdatedOnce { get; private set; }

        public static bool Boost;

        private Stopwatch Stopwatch = new Stopwatch();

        public static bool IsMainThread => MainThread == Thread.CurrentThread;


        public MainThreadHelper(Game game)
            : base(game) {
            Instance = this;

            UpdateOrder = -500000;
            MainThread = Thread.CurrentThread;
        }

        public static void Do(Action a) {
            if (IsMainThread) {
                a();
                return;
            }

            lock (Queue) {
                Queue.Enqueue(a);
            }
        }

        public static void Do(object key, Action a) {
            if (IsMainThread) {
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
        }

        public static MaybeAwaitable<T> Get<T>(Func<T> f) {
            if (IsMainThread) {
                return new MaybeAwaitable<T>(f());
            }

            lock (Queue) {
                T result = default;
                Task<T> proxy = new Task<T>(() => result);
                MaybeAwaitable<T> awaitable = new MaybeAwaitable<T>(proxy.GetAwaiter(), () => UpdatedOnce);

                Queue.Enqueue(() => {
                    result = f != null ? f.Invoke() : default;
                    proxy.Start();
                });

                return awaitable;
            }
        }

        public static MaybeAwaitable<T> Get<T>(object key, Func<T> f) {
            if (IsMainThread) {
                return new MaybeAwaitable<T>(f());
            }

            lock (Queue) {
                if (!Enqueued.Add(key))
                    return (MaybeAwaitable<T>) EnqueuedWaiting[key];

                T result = default;
                Task<T> proxy = new Task<T>(() => result);
                MaybeAwaitable<T> awaitable = new MaybeAwaitable<T>(proxy.GetAwaiter(), () => UpdatedOnce);
                EnqueuedWaiting[key] = awaitable;

                Queue.Enqueue(() => {
                    result = f != null ? f.Invoke() : default;
                    proxy.Start();
                    lock (Queue) {
                        EnqueuedWaiting.Remove(key);
                        Enqueued.Remove(key);
                    }
                });

                return awaitable;
            }
        }

        public override void Update(GameTime gameTime) {
            UpdatedOnce = true;

            if (Queue.Count > 0) {
                // run as many tasks as possible in 10 milliseconds (a frame is ~16ms).
                Stopwatch.Restart();
                while (Boost || Stopwatch.ElapsedMilliseconds < 10) {
                    Action action = null;
                    lock (Queue) {
                        if (Queue.Count > 0) {
                            action = Queue.Dequeue();
                        }
                    }
                    if (action == null)
                        break;
                    action.Invoke();
                }
                Stopwatch.Stop();
            }

            if (gameTime == null)
                return;

            base.Update(gameTime);
        }

    }

    public struct MaybeAwaitable<T> {

        private MaybeAwaiter _Awaiter;

        public MaybeAwaitable(T result) {
            _Awaiter = new MaybeAwaiter();
            _Awaiter._IsImmediate = true;
            _Awaiter._Result = result;
            _Awaiter._CanGetResult = null;
        }

        public MaybeAwaitable(TaskAwaiter<T> task) {
            _Awaiter = new MaybeAwaiter();
            _Awaiter._IsImmediate = false;
            _Awaiter._Result = default;
            _Awaiter._Task = task;
            _Awaiter._CanGetResult = null;
        }

        public MaybeAwaitable(TaskAwaiter<T> task, Func<bool> canGetResult) {
            _Awaiter = new MaybeAwaiter();
            _Awaiter._IsImmediate = false;
            _Awaiter._Result = default;
            _Awaiter._Task = task;
            _Awaiter._CanGetResult = canGetResult;
        }

        public MaybeAwaiter GetAwaiter() => _Awaiter;
        public T GetResult() => _Awaiter.GetResult();

        public struct MaybeAwaiter : ICriticalNotifyCompletion {

            internal bool _IsImmediate;
            internal T _Result;
            internal TaskAwaiter<T> _Task;
            internal Func<bool> _CanGetResult;

            public bool IsCompleted => _IsImmediate || _Task.IsCompleted;

            public T GetResult() {
                if (_IsImmediate)
                    return _Result;
                if (!(_CanGetResult?.Invoke() ?? true))
                    throw new Exception("Cannot obtain the result - potential deadlock!");
                return _Task.GetResult();
            }

            public void OnCompleted(Action continuation) {
                if (_IsImmediate) {
                    continuation();
                    return;
                }
                _Task.OnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation) {
                if (_IsImmediate) {
                    continuation();
                    return;
                }
                _Task.UnsafeOnCompleted(continuation);
            }
        }

    }
}
