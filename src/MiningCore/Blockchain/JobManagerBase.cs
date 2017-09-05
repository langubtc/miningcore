﻿/* 
Copyright 2017 Coin Foundry (coinfoundry.org)
Authors: Oliver Weichhold (oliver@weichhold.com)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
associated documentation files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial 
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using MiningCore.Blockchain.Bitcoin;
using MiningCore.Configuration;
using MiningCore.DaemonInterface;
using MiningCore.Util;
using NLog;
using Contract = MiningCore.Contracts.Contract;

namespace MiningCore.Blockchain
{
    public abstract class JobManagerBase<TJob>
    {
        protected JobManagerBase(IComponentContext ctx, DaemonClient daemon)
        {
            Contract.RequiresNonNull(ctx, nameof(ctx));
            Contract.RequiresNonNull(daemon, nameof(daemon));

            this.ctx = ctx;
            this.daemon = daemon;
        }

        protected readonly IComponentContext ctx;
        protected ClusterConfig clusterConfig;

        protected TJob currentJob;
        protected DaemonClient daemon;
        private long jobId;
        protected object jobLock = new object();
        protected ILogger logger;
        protected PoolConfig poolConfig;

        protected virtual string LogCat { get; } = "Job Manager";

        protected virtual void ConfigureDaemons()
        {
            daemon.Configure(poolConfig.Daemons);
        }

        protected virtual async Task StartDaemonAsync()
        {
            while (!await IsDaemonHealthy())
            {
                logger.Info(() => $"[{LogCat}] Waiting for daemons to come online ...");

                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            logger.Info(() => $"[{LogCat}] All daemons online");

            while (!await IsDaemonConnected())
            {
                logger.Info(() => $"[{LogCat}] Waiting for daemons to connect to peers ...");

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        protected string NextJobId()
        {
            return Interlocked.Increment(ref jobId).ToString(CultureInfo.InvariantCulture);
        }

        protected abstract Task<bool> IsDaemonHealthy();
        protected abstract Task<bool> IsDaemonConnected();
        protected abstract Task EnsureDaemonsSynchedAsync();
        protected abstract Task PostStartInitAsync();

        #region API-Surface

        public virtual void Configure(PoolConfig poolConfig, ClusterConfig clusterConfig)
        {
            Contract.RequiresNonNull(poolConfig, nameof(poolConfig));
            Contract.RequiresNonNull(clusterConfig, nameof(clusterConfig));

            logger = LogUtil.GetPoolScopedLogger(typeof(BitcoinJobManager), poolConfig);
            this.poolConfig = poolConfig;
            this.clusterConfig = clusterConfig;

            ConfigureDaemons();
        }

        public async Task StartAsync()
        {
            Contract.RequiresNonNull(poolConfig, nameof(poolConfig));

            logger.Info(() => $"[{LogCat}] Launching ...");

            await StartDaemonAsync();
            await EnsureDaemonsSynchedAsync();
            await PostStartInitAsync();

            logger.Info(() => $"[{LogCat}] Online");
        }

        #endregion // API-Surface
    }
}