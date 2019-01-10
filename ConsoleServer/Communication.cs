using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MoleculeDataBase;

namespace ConsoleServer
{
    public class Communication
    {
        public AES_Data Crypt;

        public Communication()
        {
            Crypt = AES_Data.NewAES();
        }

        /// <summary>
        /// Посылает сообщение с открывающей и закрывающей командами
        /// </summary>
        /// <param name="handler">Сокет, через который отправляется сообщение</param>
        /// <param name="Message">Текст сообщения</param>
        public void SimpleMsg(Socket handler, List<string> Messages, bool encrypt = true)
        {
            SimpleMsg(handler, Messages.ToArray(), encrypt);
        }

        /// <summary>
        /// Посылает сообщение с открывающей и закрывающей командами
        /// </summary>
        /// <param name="handler">Сокет, через который отправляется сообщение</param>
        /// <param name="Message">Текст сообщения</param>
        public void SimpleMsg(Socket handler, string[] Messages, bool encrypt=true)
        {
            string LargeText = "";
            foreach (string Message in Messages)
                LargeText += LargeText == "" ? Message : "\n" + Message;
            SimpleMsg(handler, LargeText, encrypt);
        }

        /// <summary>
        /// Отправка текстового сообщения через сокет.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="Msg"></param>
        public void SendMsg(Socket handler, string Msg, bool encrypt=true)
        {
            byte[] msg = encrypt
                ? Crypt.EncryptStringToBytes(Msg + "\n")
                : Encoding.UTF8.GetBytes(Msg + "\n");
            handler.Send(BitConverter.GetBytes(msg.Length));
            using (FileStream FS = new FileStream("temp.dat", FileMode.Create))
            {
                FS.Write(msg, 0, msg.Length);
                FS.Close();
            };
            handler.Send(msg);
        }

        /// <summary>
        /// Отсылает клиенту простое текстовое сообщение.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="Message"></param>
        public void SimpleMsg(Socket handler, string Message, bool encrypt = true)
        {
            SendMsg(handler, Commands.Answer.StartMsg + "\n" +
                             Message + "\n" +
                             Commands.Answer.EndMsg, encrypt);
        }

        /// <summary>
        /// Отправка бинарной информации
        /// </summary>
        public void SendBinaryData(Socket handler, byte[] Data, bool encrypt = true)
        {
            byte[] EncryptedData = encrypt
                ? Crypt.EncryptBytes(Data)
                : Data;
            int FtS_Size = EncryptedData.Count();
            handler.Send(BitConverter.GetBytes(FtS_Size));
            for (int i = 0; i < FtS_Size; i += 1024)
            {

                int block;
                if (FtS_Size - i < 1024) { block = FtS_Size - i; }
                else { block = 1024; }

                byte[] buf = new byte[block];
                Array.Copy(EncryptedData, i, buf, 0, block);
                handler.Send(buf);
            }
        }

        /// <summary>
        /// Отправка бинарной информации
        /// </summary>
        public void SendBinaryData(Socket handler, Stream Data, bool encrypt = true)
        {
            byte[] ByteData = new byte[Data.Length];
            Data.Position = 0;
            Data.Read(ByteData, 0, ByteData.Length);
            SendBinaryData(handler, ByteData, encrypt);
        }

        /// <summary>
        /// Отсылает клиенту данные для щифрования.
        /// </summary>
        /// <param name="handler"></param>
        public void SendKey(Socket handler)
        {
            MemoryStream MS = Crypt.ToBin();
            Console.WriteLine($"Передача данных: {MS.Length} байт.");
            FileStream FS = new FileStream("temp.dat", FileMode.Create);
            MS.CopyTo(FS);
            FS.Close();
            SendBinaryData(handler, MS, false);
        }

        /// <summary>
        /// Расшифровывает поток байт
        /// </summary>
        /// <param name="Data"></param>
        /// <returns></returns>
        public byte[] DecryptBytes(byte[] Data)
        {
            return Crypt.DecryptBytes(Data);
        }

        /// <summary>
        /// Засшифровывает строку
        /// </summary>
        /// <param name="Data"></param>
        /// <returns></returns>
        public string DecryptString(byte[] Data)
        {
            return Encoding.UTF8.GetString(DecryptBytes(Data));
        }

        /// <summary>
        /// Расшифровывает байты в поток
        /// </summary>
        /// <param name="Data"></param>
        /// <returns></returns>
        public MemoryStream DecryptStreamData(byte[] Data)
        {
            return new MemoryStream(DecryptBytes(Data));
        }
    }
}
