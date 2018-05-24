﻿//
// Copyright (c) LightBuzz Software.
// All rights reserved.
//
// http://lightbuzz.com
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
//
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
// FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
// COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
// BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS
// OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED
// AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
// LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
//

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using UnityEngine;

namespace LightBuzz.Azure
{
    /// <summary>
    /// A Unity-ready MobileServiceClient.
    /// Must be extended by a subclass.
    /// </summary>
    public abstract class LightBuzzMobileServiceClient : MobileServiceClient
    {
        /// <summary>
        /// Defines the tables in the local store. 
        /// Method must be implemented in a subclass.
        /// e.g.
        /// protected override void DefineTables()
        /// {
        ///    LocalStore.DefineTable<TodoItem/>();
        /// }
        /// </summary>
        protected abstract void DefineTables();

        /// <summary>
        /// Pulls the data from the remote Azure App Service and stores them into the local database.
        /// Method must be implemented in a subclass.
        /// e.g.
        /// public override async Task Pull()
        /// {
        ///    AppServiceTableDAO<TodoItem/> todoTableDao = new AppServiceTableDAO<TodoItem/>(this);
        ///    await todoTableDao.Pull(new CancellationToken(), "TodoItems", x => x.Id != null);
        ///  }
        /// </summary>
        public abstract Task Pull();

        /// <summary>
        /// The default database name pattern.
        /// </summary>
        protected readonly string DefaultLocalDatabaseNamePattern = "database_{0}.db";

        /// <summary>
        /// The MobileServiceSQLiteStore that connects to the local database.
        /// </summary>
        protected MobileServiceSQLiteStore LocalStore;

        /// <summary>
        /// Specifies whether the app will store data locally.
        /// </summary>
        public bool SupportsLocalStore { get; set; }

        /// <summary>
        /// The local SQLite database name.
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// The local SQLite database path.
        /// </summary>
        private string _localDatabasePath;

        /// <summary>
        /// Creates a new instance of the LightBuzzMobileServiceClient. 
        /// </summary>
        /// <param name="mobileAppUri">Azure App Service URL</param>
        /// <param name="supportLocal">Supports local database</param>
#if !UNITY_WSA || UNITY_EDITOR

        protected LightBuzzMobileServiceClient(string mobileAppUri, bool supportLocal) : base(mobileAppUri, new LightBuzzHttpsHandler())
        {
            SupportsLocalStore = supportLocal;
            DatabaseName = string.Format(DefaultLocalDatabaseNamePattern, mobileAppUri.GetHashCode());
        }
#else

        protected LightBuzzMobileServiceClient(string mobileAppUri,bool supportLocal) : base(mobileAppUri)
        {
            SupportsLocalStore = supportLocal;
            DatabaseName = string.Format(DefaultLocalDatabaseNamePattern, mobileAppUri.GetHashCode());
        }

#endif

        /// <summary>
        /// Gets the local SQLite database absolute path.
        /// </summary>
        protected string LocalDatabasePath
        {
            get
            {
                if (string.IsNullOrEmpty(_localDatabasePath))
                {
                    return Path.Combine(Application.persistentDataPath, DatabaseName);
                }

                return _localDatabasePath;
            }
        }

        /// <summary>
        /// Gets the local SQLite database absolute path.
        /// </summary>
        protected string LocalDatabaseConnectionString
        {
            get
            {
#if !UNITY_WSA || UNITY_EDITOR
                return "Data Source=" + LocalDatabasePath + ";Version=3;";
#else
                return LocalDatabasePath;
#endif
            }
        }

        /// <summary>
        /// Initializes the local SQLite database.
        /// </summary>
        /// <param name="azureClient">The Azure Client object.</param>
        /// <param name="localDatabasePath">The full path to the local database file, e.g. Path.Combine(Application.persistentDataPath, "database.db").</param>
        /// <returns></returns>
        protected async Task InitStore(LightBuzzMobileServiceClient azureClient, string localDatabasePath)
        {
            if (azureClient == null)
            {
                throw new NullReferenceException("Azure Client is null.");
            }

            _localDatabasePath = localDatabasePath;

            if (!azureClient.SyncContext.IsInitialized)
            {
                try
                {
#if !UNITY_WSA
                    if (!File.Exists(LocalDatabasePath))
                    {
                        File.Create(LocalDatabasePath).Close();
                    }
#endif

                    LocalStore = new MobileServiceSQLiteStore(LocalDatabaseConnectionString);

                    DefineTables();

                    await SyncContext.InitializeAsync(LocalStore);
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                    Debug.LogError(e.Source);
                    Debug.LogError(e.StackTrace);
                    Debug.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Initializes the local SQLite database.
        /// </summary>
        /// <param name="azureClient">The Azure Client object.</param>
        /// <returns></returns>
        protected async Task InitStore(LightBuzzMobileServiceClient azureClient)
        {
            await InitStore(azureClient, Path.Combine(Application.persistentDataPath, DatabaseName));
        }

        /// <summary>
        /// Syncs with the remote Azure App Service (pull/push operations).
        /// </summary>
        /// <returns></returns>
        protected async Task SyncStore()
        {
            try
            {
                await Push();
                await Pull();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogError(e.Source);
                Debug.LogError(e.StackTrace);
                Debug.LogError(e.ToString());
            }
        }

        /// <summary>
        /// Pushes the data stored in the local SQLite database to the remote Azure App Service.
        /// </summary>
        /// <returns></returns>
        public async Task Push()
        {
            await SyncContext.PushAsync();
        }

        /// <summary>
        /// Initializes local SQLite database, if local store is supported.
        /// </summary>
        /// <returns></returns>
        public async Task Init()
        {
            if (SupportsLocalStore)
            {
                await InitStore(this);
            }
        }

        /// <summary>
        /// Syncs with the remote Azure App Service, if local store is supported.
        /// </summary>
        /// <returns></returns>
        public async Task Sync()
        {
            if (SupportsLocalStore)
            {
                await SyncStore();
            }
        }

        /// <summary>
        /// Initializes local SQLite database connection and sync.
        /// </summary>
        /// <returns></returns>
        public async Task InitializeLocalStore()
        {
            await Init();
            await Sync();
        }
    }
}
