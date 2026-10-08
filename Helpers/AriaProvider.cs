using System;
using System.Text;

namespace AriaSecurity
{
    public class AriaProvider
    {
        #region Field

        private AriaAlgorithm aria;
        private int keySize;
        private byte[] masterKey;

        private byte[][] plainTextBlock = null;
        private byte[][] cipherTextBlock = null;

        #endregion Field

        #region Property

        /// <summary>
        /// 암복호화에 이용할 마스터 키
        /// 키사이즈에 따라 달라짐(키사이즈:마스터키.Length)
        /// (128:16, 192:24, 256:32)
        /// </summary>
        public byte[] MasterKey
        {
            get { return masterKey; }
            set
            {
                switch (this.keySize)
                {
                    case 128:
                        if (value.Length != 16)
                        {
                            throw new ArgumentException("MasterKey's Length : " + value.Length.ToString());
                        }
                        else
                        {
                            this.masterKey = value;
                        }
                        break;

                    case 192:
                        if (value.Length != 24)
                        {
                            throw new ArgumentException("MasterKey's Length : " + value.Length.ToString());
                        }
                        else
                        {
                            this.masterKey = value;
                        }
                        break;

                    case 256:
                        if (value.Length != 32)
                        {
                            throw new ArgumentException("MasterKey's Length : " + value.Length.ToString());
                        }
                        else
                        {
                            this.masterKey = value;
                        }
                        break;
                }
            }
        }

        #endregion Property

        #region 생성자

        /// <summary>
        /// 기본 키 사이즈는 128비트
        /// </summary>
        public AriaProvider()
        {
            this.keySize = 128;
            this.aria = new AriaAlgorithm(this.keySize);
        }

        /// <summary>
        /// 기본 키사이즈는 128
        /// </summary>
        /// <param name="keySize">128, 192, 256만 지원</param>
        public AriaProvider(int keySize)
        {
            this.keySize = keySize;
            this.aria = new AriaAlgorithm(this.keySize);
        }

        #endregion 생성자

        #region Private Method

        /// <summary>
        /// 마스터키의 기본값을 정의 한다.
        /// </summary>
        private void SetDefaultMasterKey()
        {
            if (this.masterKey == null)
            {
                int length = (this.keySize == 128) ? 16 : ((this.keySize == 192) ? 24 : 32);
                this.masterKey = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    this.masterKey[i] = (byte)i;
                }
            }
        }

        #region Encrypt

        /// <summary>
        /// 입력 받은 값을 암호화한다.
        /// </summary>
        /// <param name="inputText">암호화할 문자열입니다.</param>
        private void Encrypt(string inputText)
        {
            this.SetDefaultMasterKey();
            aria.SetKey(this.masterKey);
            aria.SetupRoundKeys();

            this.EncryptDivisionBlock(inputText);
            this.Encrypt();
        }

        /// <summary>
        /// 암호화를 처리 한다.
        /// </summary>
        private void Encrypt()
        {
            this.cipherTextBlock = new byte[this.plainTextBlock.Length][];
            for (int i = 0; i < this.plainTextBlock.Length; i++)
            {
                this.cipherTextBlock[i] = new byte[16];
                this.aria.Encrypt(this.plainTextBlock[i], 0, cipherTextBlock[i], 0);
            }
        }

        #endregion Encrypt

        #region Decrypt

        /// <summary>
        /// 암호화된 문자열을 복호화합니다.
        /// </summary>
        /// <param name="inputText">암호화된 문자열입니다.</param>
        private void Decrypt(string inputText)
        {
            this.SetDefaultMasterKey();
            aria.SetKey(this.masterKey);
            aria.SetupRoundKeys();

            this.DecryptDivisionBlock(inputText);
            this.Decrypt();
        }

        /// <summary>
        /// 암호화된 byte[]을 복호화합니다.
        /// </summary>
        /// <param name="inputArray">암호화된 byte[]입니다.</param>
        private void Decrypt(byte[] inputArray)
        {
            this.SetDefaultMasterKey();
            aria.SetKey(this.masterKey);
            aria.SetupRoundKeys();

            this.DecryptDivisionBlock(inputArray);
            this.Decrypt();
        }

        /// <summary>
        /// 복호화를 처리한다.
        /// </summary>
        private void Decrypt()
        {
            this.plainTextBlock = new byte[this.cipherTextBlock.Length][];
            for (int i = 0; i < this.cipherTextBlock.Length; i++)
            {
                this.plainTextBlock[i] = new byte[16];
                this.aria.Decrypt(this.cipherTextBlock[i], 0, plainTextBlock[i], 0);
            }
        }

        #endregion Decrypt

        /// <summary>
        /// 입력된 문자열을 byte[]으로 변환시킨다.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private byte[] ConvertToByteArrayFromString(string str)
        {
            byte[] temp = Encoding.Default.GetBytes(str);

            if (temp.Length % 16 != 0)
            {
                byte[] temp2 = new byte[((temp.Length / 16) + 1) * 16];
                Buffer.BlockCopy(temp, 0, temp2, 0, temp.Length);
                return temp2;
            }
            else
            {
                return temp;
            }
        }

        /// <summary>
        /// 입력된 암호화된 문자열을 byte[]으로 변환시킨다.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private byte[] EncryptedConvertToByteArrayFromString(string str)
        {
            byte[] temp = new byte[str.Length / 2];
            for (int i = 0; i < temp.Length; i++)
            {
                string hex = str.Substring(i * 2, 2);
                temp[i] = Convert.ToByte(Convert.ToInt32(hex, 16));
            }

            if (temp.Length % 16 != 0)
            {
                byte[] temp2 = new byte[((temp.Length / 16) + 1) * 16];
                Buffer.BlockCopy(temp, 0, temp2, 0, temp.Length);
                return temp2;
            }
            else
            {
                return temp;
            }
        }

        /// <summary>
        /// 입력된 문자열을 byte[]에 16바이트로 나누어서 블럭 카피 한다.
        /// </summary>
        /// <param name="plainText"></param>
        private void EncryptDivisionBlock(string plainText)
        {
            byte[] temp = this.ConvertToByteArrayFromString(plainText);
            int index = (temp.Length - 1) / 16 + 1;
            this.plainTextBlock = new byte[index][];
            for (int i = 0, j = 0; j < index; i += 16, j++)
            {
                this.plainTextBlock[j] = new byte[16];
                Buffer.BlockCopy(temp, i, this.plainTextBlock[j], 0, 16);
            }
        }

        /// <summary>
        /// 입력된 암호화된 문자열을 byte[]에 16바이트로 나누어서 블럭 카피 한다.
        /// </summary>
        /// <param name="cipherText"></param>
        private void DecryptDivisionBlock(string cipherText)
        {
            byte[] temp = this.EncryptedConvertToByteArrayFromString(cipherText);
            int index = (temp.Length - 1) / 16 + 1;
            this.cipherTextBlock = new byte[index][];
            for (int i = 0, j = 0; j < index; i += 16, j++)
            {
                this.cipherTextBlock[j] = new byte[16];
                Buffer.BlockCopy(temp, i, this.cipherTextBlock[j], 0, 16);
            }
        }

        /// <summary>
        /// 입력된 암호화된 byte[]값을 byte[]에 16바이트로 나누어서 블럭 카피 한다.
        /// </summary>
        /// <param name="cipherByteArray"></param>
        private void DecryptDivisionBlock(byte[] cipherByteArray)
        {
            int index = (cipherByteArray.Length - 1) / 16 + 1;
            this.cipherTextBlock = new byte[index][];
            for (int i = 0, j = 0; j < index; i += 16, j++)
            {
                this.cipherTextBlock[j] = new byte[16];
                Buffer.BlockCopy(cipherByteArray, i, this.cipherTextBlock[j], 0, 16);
            }
        }

        #endregion Private Method

        #region Public Method

        /// <summary>
        /// 문자열을 받아 암호화된 문자열을 반환한다.
        /// </summary>
        /// <param name="inputString">암호화할 문자열입니다.</param>
        /// <returns>암호화된 문자열입니다.</returns>
        public string EncryptToString(string inputString)
        {
            if (Utils.StringUtil.IsNullOrWhiteSpace(inputString))
            {
                return inputString;
            }
            else
            {
                this.Encrypt(inputString);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < this.cipherTextBlock.Length; i++)
                {
                    sb.Append(BitConverter.ToString(this.cipherTextBlock[i]).Replace("-", ""));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 문자열을 받아 암호화된 byte[]을 반환한다.
        /// </summary>
        /// <param name="inputString">암호화할 문자열입니다.</param>
        /// <returns>암호화된 byte[]입니다.</returns>
        public byte[] EncryptToByteArray(string inputString)
        {
            if (Utils.StringUtil.IsNullOrWhiteSpace(inputString))
            {
                return null;
            }
            else
            {
                this.Encrypt(inputString);

                byte[] retValue = new byte[this.cipherTextBlock.Length * 16];
                for (int i = 0, j = 0; i < this.cipherTextBlock.Length; i++, j += 16)
                {
                    Buffer.BlockCopy(this.cipherTextBlock[i], 0, retValue, j, 16);
                }
                return retValue;
            }
        }

        /// <summary>
        /// 암호화된 문자열을 받아 복호화한다.
        /// </summary>
        /// <param name="inputString">암호화된 문자열입니다.</param>
        /// <returns>복호화된 문자열입니다.</returns>
        public string DecryptFromString(string inputString)
        {
            byte[] dest = new byte[inputString.Length];
            if (Utils.StringUtil.IsNullOrWhiteSpace(inputString))
            {
                return inputString;
            }
            else
            {
                this.Decrypt(inputString);

                for (int i = 0, j = 0; i < this.plainTextBlock.Length; i++, j += 16)
                {
                    Buffer.BlockCopy(this.plainTextBlock[i], 0, dest, j, 16);
                }
                return Encoding.Default.GetString(dest, 0, dest.Length);
            }
        }

        /// <summary>
        /// 암호화된 byte[]을 받아 복호화한다.
        /// </summary>
        /// <param name="inputByteArray">암호화된 byte[]입니다.</param>
        /// <returns>복호화된 문자열입니다.</returns>
        public string DecryptFromByteArray(byte[] inputByteArray)
        {
            byte[] dest = new byte[inputByteArray.Length];
            if (inputByteArray == null || inputByteArray.Length == 0)
            {
                return string.Empty;
            }
            else
            {
                this.Decrypt(inputByteArray);

                for (int i = 0, j = 0; i < this.plainTextBlock.Length; i++, j += 16)
                {
                    Buffer.BlockCopy(this.plainTextBlock[i], 0, dest, j, 16);
                }
                return Encoding.Default.GetString(dest, 0, dest.Length);
            }
        }

        #endregion Public Method
    }
}