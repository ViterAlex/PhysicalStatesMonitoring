﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Timers;

namespace Itp
{
    /// <summary>Класс для чтения данных со СКУД.</summary>
    public class DataReader
    {
        /// <summary>Событие при чтении данных со СКУД.</summary>
        /// <remarks>Событие возникает каждый раз, когда данные читаются. Это не означает, что они изменились в СКУД.</remarks>
        public event EventHandler<DataReadEventArgs> DataRead;
        /// <summary>Событие при изменении состояния ридера. <see cref="ReaderStateEnum"/>.</summary>
        public event EventHandler<ReaderStateChangedEventArgs> StateChanged;
        /// <summary>Событие при любых ошибках ридера.</summary>
        public event EventHandler<DataReaderErrorEventArgs> ErrorOccured;

        private readonly Timer _timer;
        private ReaderStateEnum _readerState;
        private IptReader _iptReader;
        private ScudReader _scudReader;

        //Интервал чтения данных
        public double Interval
        {
            get { return _timer.Interval; }
            set { _timer.Interval = value; }
        }

        /// <summary>Состояние ридера. <see cref="ReaderStateEnum"/></summary>
        /// <remarks>Возможные состояния ридера:
        /// <para><see cref="ReaderStateEnum.Connected"/> — ридер соединён со СКУД. Устанавливается извне.</para>
        /// <para><see cref="ReaderStateEnum.Disconnected"/> — ридер отсоединён от СКУД. Устанавливается извне.</para>
        /// <para><see cref="ReaderStateEnum.DataReading"/> — ридер читает данные со СКУД.</para>
        /// </remarks>
        public ReaderStateEnum ReaderState
        {
            get { return _readerState; }
            set
            {
                if (_readerState == value) return;
                _readerState = value;
                OnStateChanged(new ReaderStateChangedEventArgs(_readerState));
            }
        }

        public DataReader()
        {
            _readerState = ReaderStateEnum.Disconnected;
            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            MbCliWrapper.Connected += (s, e) =>
            {
                ReaderState = ReaderStateEnum.Connected;
            };
            MbCliWrapper.Disconnected += (s, e) =>
            {
                ReaderState = ReaderStateEnum.Disconnected;
            };
            MbCliWrapper.ErrorOccured += (s, e) =>
            {
                OnErrorOccured(new DataReaderErrorEventArgs(e.ErrorCode, string.Format("Ошибка СКУД.\n{0}", e.InternalMessage)));
            };
        }

        /// <summary>Начать чтение данных.</summary>
        public void Start()
        {
            if (ReaderState == ReaderStateEnum.Disconnected)
            {
                Debug.WriteLine("Не подсоединён");
                return;
            }
            _timer.Start();
            ReaderState = ReaderStateEnum.DataReading;
        }

        /// <summary>Остановить чтение данных.</summary>
        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>Соединение с ИПТ и СКУД.</summary>
        public void Connect(IPAddress scudAddress, int scudPort, IPAddress iptAddress, int iptPort)
        {
            //TODO: Состояние соединения должно зависеть от результат соединения с ИПТ и со СКУД. Сейчас зависит только от СКУД
            ConnectIpt(iptAddress, iptPort);
            ReaderState = ReaderStateEnum.Connected;
            ConnectScud(scudAddress, scudPort);
        }

        /// <summary>Отсоединение от ИПТ и СКУД.</summary>
        public void Disconnect()
        {
            Stop();
            DisconnectScud();
            DisconnectIpt();
        }

        private void ConnectIpt(IPAddress address, int port)
        {
            _iptReader = new IptReader(address, port);
            _iptReader.Connect();
            //throw new NotImplementedException();
        }

        private void ConnectScud(IPAddress address, int port)
        {
            _scudReader = new ScudReader(address, port);
        }

        private void DisconnectScud()
        {
            _scudReader.Disconnect();
            _scudReader = null;
        }

        //TODO:Добавить реализацию отсоединения от ИПТ
        private void DisconnectIpt()
        {
            _iptReader.Disconnect();
            _iptReader = null;
            Debug.WriteLine("DisconnectIpt();");
            //throw new NotImplementedException();
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Read();
        }

        public void Read()
        {
            var buff = _scudReader.Read();
            var ipt = _iptReader.Read();
            //Вызов события
            OnDataRead(new DataReadEventArgs(buff, ipt));
        }

        protected virtual void OnDataRead(DataReadEventArgs e)
        {
            EventHandler<DataReadEventArgs> handler = DataRead;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnStateChanged(ReaderStateChangedEventArgs e)
        {
            EventHandler<ReaderStateChangedEventArgs> handler = StateChanged;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnErrorOccured(DataReaderErrorEventArgs e)
        {
            EventHandler<DataReaderErrorEventArgs> handler = ErrorOccured;
            if (handler != null) handler(this, e);
        }
    }
}
