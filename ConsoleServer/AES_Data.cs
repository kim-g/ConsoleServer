using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace ConsoleServer
{
    public class AES_Data
    {
        public AES_Data()
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
        public byte[] AesIV;
        public byte[] AesKey;

        public void SaveToFile(string FileName)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AES_Data));
            using (FileStream fs = File.OpenWrite(FileName))
            {
                serializer.Serialize(fs, this);
            }
        }

        static public AES_Data LoadFromFile(string FileName)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AES_Data));
            using (FileStream fs = File.OpenRead(FileName))
            {
                AES_Data temp = (AES_Data)serializer.Deserialize(fs);
                return temp;
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
            // Получаем поток байтов и шифруем.
            byte[] encrypted = EncryptBytes(Encoding.UTF8.GetBytes(plainText));

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
                        csDecrypt.Read(Info, 0, Convert.ToInt32( csDecrypt.Length));
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
