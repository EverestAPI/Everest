using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using Monocle;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Celeste.Mod {
    public class MainThreadHelper : GameComponent {

        public static MainThreadHelper Instance;

        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static readonly HashSet<object> Enqueued = new HashSet<object>();
        private static readonly Dictionary<object, object> EnqueuedWaiting = new Dictionary<object, object>();
        public static Thread MainThread { get; private set; }

        public MainThreadHelper(Game game)
            : base(game) {
            Instance = this;

            UpdateOrder = -500000;
            MainThread = Thread.CurrentThread;
        }

        public static void Do(Action a) {
            if (Thread.CurrentThread == MainThread) {
                a();
                return;
            }

            lock (Queue) {
                Queue.Enqueue(a);
            }
        }

        public static void Do(object key, Action a) {
            if (Thread.CurrentThread == MainThread) {
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
            if (Thread.CurrentThread == MainThread) {
                return new MaybeAwaitable<T>(f());
            }

            lock (Queue) {
                T result = default(T);
                Task<T> proxy = new Task<T>(() => result);
                MaybeAwaitable<T> awaitable = new MaybeAwaitable<T>(proxy.GetAwaiter());

                Queue.Enqueue(() => {
                    result = f != null ? f.Invoke() : default(T);
                    proxy.Start();
                });

                return awaitable;
            }
        }

        public static MaybeAwaitable<T> Get<T>(object key, Func<T> f) {
            if (Thread.CurrentThread == MainThread) {
                return new MaybeAwaitable<T>(f());
            }

            lock (Queue) {
                if (!Enqueued.Add(key))
                    return (MaybeAwaitable<T>) EnqueuedWaiting[key];

                T result = default(T);
                Task<T> proxy = new Task<T>(() => result);
                MaybeAwaitable<T> awaitable = new MaybeAwaitable<T>(proxy.GetAwaiter());
                EnqueuedWaiting[key] = awaitable;

                Queue.Enqueue(() => {
                    result = f != null ? f.Invoke() : default(T);
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
            while (Queue.Count > 0) {
                Action action;
                lock (Queue) {
                    action = Queue.Dequeue();
                }
                action?.Invoke();
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
        }

        public MaybeAwaitable(TaskAwaiter<T> task) {
            _Awaiter = new MaybeAwaiter();
            _Awaiter._IsImmediate = false;
            _Awaiter._Result = default(T);
            _Awaiter._Task = task;
        }

        public MaybeAwaiter GetAwaiter() => _Awaiter;
        public T GetResult() => _Awaiter.GetResult();

        public struct MaybeAwaiter : ICriticalNotifyCompletion {

            internal bool _IsImmediate;
            internal T _Result;
            internal TaskAwaiter<T> _Task;

            public bool IsCompleted => _IsImmediate || _Task.IsCompleted;

            public T GetResult() {
                if (_IsImmediate)
                    return _Result;
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
