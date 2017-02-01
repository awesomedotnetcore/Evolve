﻿using Evolve.Utilities;
using System;
using System.Data;

namespace Evolve.Connection
{
    public class WrappedConnection : IWrappedConnection
    {
        private readonly bool _connectionOwned;
        private int? _commandTimeout;
        private int _openedCount;
        private bool _openedInternally;
        private const string InvalidCommandTimeout = "The specified CommandTimeout value is not valid. It must be a positive number.";
        private const string TransactionAlreadyStarted = "The connection is already in a transaction and cannot participate in another transaction.";
        private const string NoActiveTransaction = "The connection does not have any active transactions.";

        public WrappedConnection(IDbConnection connection, bool connectionOwned = true)
        {
            DbConnection = Check.NotNull(connection, nameof(connection));
            _connectionOwned = connectionOwned;
        }

        public IDbConnection DbConnection { get; private set; }

        public IDbTransaction CurrentTx { get; private set; }

        public virtual int? CommandTimeout
        {
            get { return _commandTimeout; }
            set
            {
                if (value.HasValue && value < 0)
                {
                    throw new ArgumentException(InvalidCommandTimeout);
                }

                _commandTimeout = value;
            }
        }

        public IDbTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.Unspecified)
        {
            if (CurrentTx != null)
            {
                throw new InvalidOperationException(TransactionAlreadyStarted);
            }

            Open();

            CurrentTx = DbConnection.BeginTransaction(isolationLevel);
            return CurrentTx;
        }

        public void Commit()
        {
            if (CurrentTx == null)
            {
                throw new InvalidOperationException(NoActiveTransaction);
            }

            CurrentTx.Commit();
        }

        public void Rollback()
        {
            if (CurrentTx == null)
            {
                throw new InvalidOperationException(NoActiveTransaction);
            }

            CurrentTx.Rollback();
        }

        public void Open()
        {
            if (DbConnection.State == ConnectionState.Broken)
            {
                DbConnection.Close();
            }

            if (DbConnection.State != ConnectionState.Open)
            {
                DbConnection.Open();

                if (_openedCount == 0)
                {
                    _openedInternally = true;
                    _openedCount++;
                }
            }
            else
            {
                _openedCount++;
            }
        }

        public void Close()
        {
            if (_openedCount > 0 && --_openedCount == 0 && _openedInternally)
            {
                if (DbConnection.State != ConnectionState.Closed)
                {
                    DbConnection.Close();
                }
                _openedInternally = false;
            }
        }

        public void Dispose()
        {
            CurrentTx?.Dispose();

            if (_connectionOwned)
            {
                DbConnection.Dispose();
                _openedCount = 0;
            }
        }
    }
}