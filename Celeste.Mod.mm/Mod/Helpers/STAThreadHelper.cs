using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class STAThreadHelper : GameComponent {

        public static STAThreadHelper Instance;
        public static STAThreadHelper InstanceSafe => Instance ?? throw new InvalidOperationException("STAThreadHelper is currently not running");

        private static bool IsWorkerThread => Thread.CurrentThread == InstanceSafe.TaskScheduler.WorkerThread;

        private static ConcurrentDictionary<object, Task> KeyedTasks = new ConcurrentDictionary<object, Task>();

        private readonly WorkerThreadTaskScheduler TaskScheduler;
        private readonly TaskFactory TaskFactory;

        public STAThreadHelper(Game game)
            : base(game) {
            Instance = this;

            UpdateOrder = -500000;

            TaskScheduler = new WorkerThreadTaskScheduler("Everest STAThread Worker", autoStart: false);
            TaskFactory = new TaskFactory(TaskScheduler.CancellationToken);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                TaskScheduler.WorkerThread.SetApartmentState(ApartmentState.STA);
            TaskScheduler.WorkerThread.Start();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            TaskScheduler.Dispose();
        }

        public static ValueTask Schedule(Action act) {
            if (IsWorkerThread) {
                act();
                return ValueTask.CompletedTask;
            } else
                return new ValueTask(InstanceSafe.TaskFactory.StartNew(act));
        }

        public static ValueTask<T> Schedule<T>(Func<T> act) {
            if (IsWorkerThread)
                return new ValueTask<T>(act());
            else
                return new ValueTask<T>(InstanceSafe.TaskFactory.StartNew(act));
        }

        public static Task Schedule(Func<Task> act) {
            if (IsWorkerThread)
                return act();
            else
                return InstanceSafe.TaskFactory.StartNew(() => act()).Unwrap();
        }

        public static Task<T> Schedule<T>(Func<Task<T>> act) {
            if (IsWorkerThread)
                return act();
            else
                return InstanceSafe.TaskFactory.StartNew(() => act()).Unwrap();
        }

        [Obsolete("Use Schedule instead")] public static void Do(Action a) => Schedule(a);
        [Obsolete("Use Schedule instead")]
        public static void Do(object key, Action act) {
            if (IsWorkerThread) {
                act();
                return;
            }

            if (KeyedTasks.TryGetValue(key, out Task task) && !task.IsCompleted)
                return;

            lock (KeyedTasks) {
                if (KeyedTasks.TryGetValue(key, out task) && task.IsCompleted)
                    return;

                KeyedTasks[key] = Schedule(act).AsTask();
            }
        }

        [Obsolete("Use Schedule instead")] public static MaybeAwaitable<T> Get<T>(Func<T> f) => new MaybeAwaitable<T>(Schedule(f));
        [Obsolete("Use Schedule instead")]
        public static MaybeAwaitable<T> Get<T>(object key, Func<T> act) {
            if (IsWorkerThread)
                return new MaybeAwaitable<T>(act());

            if (!KeyedTasks.TryGetValue(key, out Task task) || task.IsCompleted) {
                lock (KeyedTasks) {
                    if (!KeyedTasks.TryGetValue(key, out task) || task.IsCompleted)
                        KeyedTasks[key] = task = Schedule(act).AsTask();
                }
            }

            return new MaybeAwaitable<T>(((Task<T>) task).GetAwaiter());
        }

    }
}
