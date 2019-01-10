using ConsoleServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Commands
{
    /// <summary>
    /// Различные команды, не относящиеся к общим классам, а также команды для дебаггинга
    /// </summary>
    class Global : ExecutableCommand, ILogCommand
    {
        public const string Help = "help";      // Справка по консоли администратора

        public Global(DB dataBase) : base(dataBase)
        {
            Name = "";
        }

        /// <summary>
        /// Выполнение операций классом. 
        /// </summary>
        /// <param name="handler">Сокет, через который посылается ответ</param>
        /// <param name="CurUser">Пользователь</param>
        /// <param name="DataBase">База данных, из которой берётся информация</param>
        /// <param name="Command">Операция для выполнения</param>
        /// <param name="Params">Параметры операции</param>
        public void Execute(Socket handler, User CurUser, string[] Command, string[] Params, int LogID)
        {
            if (Command[0].Length == 0)
            {
                SendHelp(handler, CurUser);
                return;
            }

            switch (Command[0].ToLower())
            {
                case Help: SendHelp(handler, CurUser); break;
                default:
                    Log.AddToQueryLog(DataBase, LogID, "! Unknown command");
                    CurUser.Transport.SimpleMsg(handler, "Error 1: Unknown command in line 0");
                    break;
            }
        }

        /// <summary>
        /// Показывает справку о команде
        /// </summary>
        /// <param name="handler"></param>
        private static void SendHelp(Socket handler, User CurUser) => 
            CurUser.Transport.SimpleMsg(handler, @"Administrator's console. Gives the direct access to server. Possible comands:
 - log - direct access to server's logs;
 - database - direct access to server's database;
 - users - direct access to list of users
 - molecules - direct access to molecules list;
 - account - commands to log in and quit
 - laboratories - direct access to list of laboratories");
    }
}
