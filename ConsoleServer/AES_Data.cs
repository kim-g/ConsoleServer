using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Soap;

namespace ConsoleServer
{
    [Serializable]
    public class AES_Data111
    {
        public byte[] AesIV;
        public byte[] AesKey;


        public AES_Data111()
        {
        }

        public void CreateData()
        {
            RNGCryptoServiceProvider r = new RNGCryptoServiceProvider();
            AesKey = new byte[0x20];
            AesIV = new byte[0x10];
            r.GetNonZeroBytes(AesKey);
            r.GetNonZeroBytes(AesIV);
        }

        /// <summary>
        /// Создание нового блока AES с новым вектором шифрования.
        /// </summary>
        /// <returns></returns>
        public static AES_Data NewAES()
        {
            AES_Data New = new AES_Data();
            New.CreateData();
            return New;
        }

        public void SaveToFile(string FileName)
        {
            using (FileStream fs = File.OpenWrite(FileName))
            {
                MemoryStream ms = ToBin();
                ms.CopyTo(fs);
            }
        }

        /// <summary>
        /// Сериализация в битовый формат
        /// </summary>
        /// <returns></returns>
        public MemoryStream ToBin()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            // получаем поток, куда будем записывать сериализованный объект
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, this);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Сериализация в SOAP формат
        /// </summary>
        /// <returns></returns>
        public string ToSOAP()
        {
            SoapFormatter formatter = new SoapFormatter();
            // получаем поток, куда будем записывать сериализованный объект
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, this);
            ms.Position = 0;
            byte[] ResByte = new byte[ms.Length];
            ms.Read(ResByte, 0, ResByte.Length);
            return Encoding.UTF8.GetString(ResByte);
        }

        /// <summary>
        /// Сериализация в XML поток
        /// </summary>
        /// <returns></returns>
        public MemoryStream ToXMLStream()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AES_Data));
            MemoryStream ms = new MemoryStream();
            serializer.Serialize(ms, this);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Сериализация в XML
        /// </summary>
        /// <returns></returns>
        public string ToXML()
        {
            MemoryStream ms = ToXMLStream();
            byte[] buff = new byte[ms.Length];
            ms.Read(buff, 0, buff.Length);
            return Encoding.UTF8.GetString(buff);
        }

        /// <summary>
        /// Десериализация из битового формата
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        public static AES_Data FromBin(Stream ms)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            // получаем поток, куда будем записывать сериализованный объект
            ms.Position = 0;
            return (AES_Data)formatter.Deserialize(ms);
        }

        /// <summary>
        /// Десериализация из SOAP формата
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        public static AES_Data FromSOAP(Stream ms)
        {
            SoapFormatter formatter = new SoapFormatter();
            // получаем поток, куда будем записывать сериализованный объект
            ms.Position = 0;
            return (AES_Data)formatter.Deserialize(ms);
        }

        /// <summary>
        /// Десериализация из SOAP формата
        /// </summary>
        /// <param name="SOAP"></param>
        /// <returns></returns>
        public static AES_Data FromSOAP(string SOAP)
        {
            return FromSOAP(new MemoryStream(Encoding.UTF8.GetBytes(SOAP)));
        }

        /// <summary>
        /// Десериализация из XML
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        public static AES_Data From_XML(MemoryStream ms)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AES_Data));
            ms.Position = 0;
            return (AES_Data)serializer.Deserialize(ms);
        }

        public static AES_Data From_XML(string Data)
        {
            return From_XML(new MemoryStream(Encoding.UTF8.GetBytes(Data)));
        }

        static public AES_Data LoadFromFile(string FileName)
        {
            using (FileStream fs = File.OpenRead(FileName))
            {
                return FromBin(fs);
            }
        }

        // Щифрует набор байт, используя текущие ключ и IV
        public byte[] EncryptBytes(byte[] Info)
        {
            // Проверка аргументов
            if (Info == null || Info.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (AesKey == null || AesKey.Length <= 0)
                throw new ArgumentNullException("Key");
            if (AesIV == null || AesIV.Length <= 0)
                throw new ArgumentNullException("IV");
            byte[] encrypted;

            // Создаем объект класса AES
            // с определенным ключом and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = AesKey;
                aesAlg.IV = AesIV;

                // Создаем объект, который определяет основные операции преобразований.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Создаем поток для шифрования.
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(Info, 0, Info.Length);
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }


            //Возвращаем зашифрованные байты из потока памяти.
            return encrypted;
        }


        // Шифрует строку, используя текущие ключ и IV
        public byte[] EncryptStringToBytes(string plainText)
        {
            // Проверка аргументов
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (AesKey == null || AesKey.Length <= 0)
                throw new ArgumentNullException("Key");
            if (AesIV == null || AesIV.Length <= 0)
                throw new ArgumentNullException("IV");
            byte[] encrypted;

            // Создаем объект класса AES
            // с определенным ключом and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = AesKey;
                aesAlg.IV = AesIV;

                // Создаем объект, который определяет основные операции преобразований.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Создаем поток для шифрования.
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Записываем в поток все данные.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }


            //Возвращаем зашифрованные байты из потока памяти.
            return encrypted;

        }

        // Дешифрует массив байт, используя текущие ключ и IV
        public byte[] DecryptBytes(byte[] cipherText)
        {
            // Проверяем аргументы
            if (cipherText == null || cipherText.Length <= 0)
            {
                return null;
            };
            if (AesKey == null || AesKey.Length <= 0)
                throw new ArgumentNullException("Key");
            if (AesIV == null || AesIV.Length <= 0)
                throw new ArgumentNullException("IV");

            // Строка, для хранения расшифрованного текста
            byte[] Info;

            // Создаем объект класса AES,
            // Ключ и IV
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = AesKey;
                aesAlg.IV = AesIV;

                // Создаем объект, который определяет основные операции преобразований.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Создаем поток для расшифрования.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {

                        Info = new byte[csDecrypt.Length];
                        csDecrypt.Read(Info, 0, Convert.ToInt32(csDecrypt.Length));
                    }
                }
            }

            return Info;

        }

        // Дешифрует строку, используя текущие ключ и IV
        public string DecryptStringFromBytes(byte[] cipherText)
        {
            /*string plaintext = Encoding.UTF8.GetString(DecryptBytes(cipherText));

            return plaintext;*/

            // Проверяем аргументы
            if (cipherText == null || cipherText.Length <= 0)
            {
                return "";
            };
            if (AesKey == null || AesKey.Length <= 0)
                throw new ArgumentNullException("Key");
            if (AesIV == null || AesIV.Length <= 0)
                throw new ArgumentNullException("IV");

            // Строка, для хранения расшифрованного текста
            string plaintext;

            // Создаем объект класса AES,
            // Ключ и IV
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = AesKey;
                aesAlg.IV = AesIV;

                // Создаем объект, который определяет основные операции преобразований.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Создаем поток для расшифрования.
                using (var msDecrypt = new MemoryStream(cipherText))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Читаем расшифрованное сообщение и записываем в строку
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }

            return plaintext;

        }
    }
}
