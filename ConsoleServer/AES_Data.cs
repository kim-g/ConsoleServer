using System;
using System.IO;
using System.Security.Cryptography;
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
    }
}
