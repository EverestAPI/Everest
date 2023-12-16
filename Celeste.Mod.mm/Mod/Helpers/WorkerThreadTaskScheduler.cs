using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.Helpers {
    public class WorkerThreadTaskScheduler : TaskScheduler, IDisposable {

        public readonly Thread WorkerThread;
        private readonly CancellationTokenSource cancelSrc = new CancellationTokenSource();
        private readonly BlockingCollection<Task> tasks = new BlockingCollection<Task>();
        private bool isDisposed;

        public bool HasWork => tasks.Count > 0;
        public CancellationToken CancellationToken => cancelSrc.Token;
        public override int MaximumConcurrencyLevel => 1;

        public WorkerThreadTaskScheduler(string name, bool autoStart = true) {
            // Create a worker thread
            WorkerThread = new Thread(WorkerThreadFunc) { Name = name, IsBackground = true };

            if (autoStart)
                WorkerThread.Start();
        }

        public void Dispose() {
            if (!isDisposed)
                return;

            // Wait for the worker thread to exit
            cancelSrc.Cancel();
            tasks.CompleteAdding();

            WorkerThread.Join();

            cancelSrc.Dispose();
            tasks.Dispose();

            isDisposed = true;
        }

        private void WorkerThreadFunc() {
            CancellationToken token = cancelSrc.Token;
            try {
                while (!token.IsCancellationRequested) {
                    // Get a task from the task queue and execute it
                    TryExecuteTask(tasks.Take(token));
                }
            } catch (OperationCanceledException ex) {
                if (ex.CancellationToken != token)
                    throw;
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks() => tasks;
        protected override void QueueTask(Task task) => tasks.Add(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
            // Check if we are on the worker thread
            // We don't have to worry about dequeuing as there only is a single thread executing tasks
            if (Thread.CurrentThread != WorkerThread)
                return false;

            return TryExecuteTask(task);
        }

    }
}