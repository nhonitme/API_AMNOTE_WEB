using System;

namespace AriaSecurity
{
    internal class AriaAlgorithm
    {
        #region Field

        private int[][] KRK ={
            new int[]{(int)0x517cc1b7, unchecked((int)0x27220a94), unchecked((int)0xfe13abe8), unchecked((int)0xfa9a6ee0)},
            new int[]{(int)0x6db14acc, unchecked((int)0x9e21c820), unchecked((int)0xff28b1d5), unchecked((int)0xef5de2b0)},
            new int[]{unchecked((int)0xdb92371d), (int)0x2126e970, (int)0x03249775, (int)0x04e8c90e}
        };

        private byte[] S1 = new byte[0x100];
        private byte[] S2 = new byte[0x100];
        private byte[] X1 = new byte[0x100];
        private byte[] X2 = new byte[0x100];

        private int[] TS1 = new int[0x100];
        private int[] TS2 = new int[0x100];
        private int[] TX1 = new int[0x100];
        private int[] TX2 = new int[0x100];

        private int keySize = 0;
        private int numberOfRounds = 0;
        private byte[] masterKey = null;
        private int[] encRoundKeys = null;
        private int[] decRoundKeys = null;

        #endregion Field

        #region Initialize

        private void Initialize()
        {
            int[] exp = new int[256];
            int[] log = new int[256];
            exp[0] = 1;

            for (int i = 1; i < 256; i++)
            {
                int j = (exp[i - 1] << 1) ^ exp[i - 1];
                if ((j & 0x100) != 0) j ^= 0x11b;
                exp[i] = j;
            }
            for (int i = 1; i < 255; i++)
            {
                log[exp[i]] = i;
            }

            int[][] A = {
                new int[]{1, 0, 0, 0, 1, 1, 1, 1},
                new int[]{1, 1, 0, 0, 0, 1, 1, 1},
                new int[]{1, 1, 1, 0, 0, 0, 1, 1},
                new int[]{1, 1, 1, 1, 0, 0, 0, 1},
                new int[]{1, 1, 1, 1, 1, 0, 0, 0},
                new int[]{0, 1, 1, 1, 1, 1, 0, 0},
                new int[]{0, 0, 1, 1, 1, 1, 1, 0},
                new int[]{0, 0, 0, 1, 1, 1, 1, 1}
            };
            int[][] B = {
                new int[]{0, 1, 0, 1, 1, 1, 1, 0},
                new int[]{0, 0, 1, 1, 1, 1, 0, 1},
                new int[]{1, 1, 0, 1, 0, 1, 1, 1},
                new int[]{1, 0, 0, 1, 1, 1, 0, 1},
                new int[]{0, 0, 1, 0, 1, 1, 0, 0},
                new int[]{1, 0, 0, 0, 0, 0, 0, 1},
                new int[]{0, 1, 0, 1, 1, 1, 0, 1},
                new int[]{1, 1, 0, 1, 0, 0, 1, 1}
            };

            for (int i = 0; i < 256; i++)
            {
                int t = 0, p;
                if (i == 0)
                {
                    p = 0;
                }
                else
                {
                    p = exp[255 - log[i]];
                }
                for (int j = 0; j < 8; j++)
                {
                    int s = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        if (((p >> ((7 - k) & 0x1f)) & 1) != 0)
                        {
                            s ^= A[k][j];
                        }
                    }
                    t = (t << 1) ^ s;
                }
                t ^= 0x63;
                this.S1[i] = (byte)t;
                this.X1[t] = (byte)i;
            }

            for (int i = 0; i < 256; i++)
            {
                int t = 0;
                int p;
                if (i == 0)
                {
                    p = 0;
                }
                else
                {
                    p = exp[(0xf7 * log[i]) % 0xff];
                }
                for (int j = 0; j < 8; j++)
                {
                    int s = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        if (((p >> (k & 0x1f)) & 1) != 0)
                        {
                            s ^= B[7 - j][k];
                        }
                    }
                    t = (t << 1) ^ s;
                }
                t ^= 0xe2;
                this.S2[i] = (byte)t;
                this.X2[t] = (byte)i;
            }

            for (int i = 0; i < 256; i++)
            {
                this.TS1[i] = 0x00010101 * (this.S1[i] & 0xff);
                this.TS2[i] = 0x01000101 * (this.S2[i] & 0xff);
                this.TX1[i] = 0x01010001 * (this.X1[i] & 0xff);
                this.TX2[i] = 0x01010100 * (this.X2[i] & 0xff);
            }
        }

        #endregion Initialize

        #region Constructor

        internal AriaAlgorithm(int keySize)
        {
            this.Initialize();
            this.SetKeySize(keySize);
        }

        #endregion Constructor

        internal void SetKeySize(int keySize)
        {
            this.Reset();
            if (keySize != 0x80 && keySize != 0xc0 && keySize != 0x100)
            {
                throw new ArgumentException("keySize=" + keySize);
            }
            this.keySize = keySize;
            switch (keySize)
            {
                case 0x80:
                    this.numberOfRounds = 12;
                    break;

                case 0xc0:
                    this.numberOfRounds = 14;
                    break;

                case 0x100:
                    this.numberOfRounds = 16;
                    break;
            }
        }

        /// <summary>
        /// Resets the class so that it can be reused for another master key.
        /// </summary>
        internal void Reset()
        {
            this.keySize = 0;
            this.numberOfRounds = 0;
            this.masterKey = null;
            this.encRoundKeys = null;
            this.decRoundKeys = null;
        }

        internal int GetKeySize()
        {
            return this.keySize;
        }

        internal void SetKey(byte[] masterKey)
        {
            if (masterKey.Length * 8 < keySize)
            {
                throw new ArgumentException("masterKey size=" + masterKey.Length);
            }
            this.decRoundKeys = null;
            this.encRoundKeys = null;
            this.masterKey = (byte[])masterKey.Clone();
        }

        internal void SetupEncRoundKeys()
        {
            if (this.keySize == 0)
            {
                throw new ArgumentException("keySize");
            }
            if (this.masterKey == null)
            {
                throw new ArgumentException("masterKey");
            }
            if (this.encRoundKeys == null)
            {
                int t = 4 * (this.numberOfRounds + 1);
                if (t < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }
                this.encRoundKeys = new int[t];
            }
            this.decRoundKeys = null;
            this.DoEncKeySetup(this.masterKey, this.encRoundKeys, this.keySize);
        }

        internal void SetupDecRoundKeys()
        {
            if (this.keySize == 0)
            {
                throw new ArgumentException("keySize");
            }
            if (this.encRoundKeys == null)
            {
                if (this.masterKey == null)
                {
                    throw new ArgumentException("masterKey");
                }
                else
                {
                    this.SetupEncRoundKeys();
                }
            }
            this.decRoundKeys = (int[])encRoundKeys.Clone();
            this.DoDecKeySetup(this.masterKey, this.decRoundKeys, this.keySize);
        }

        internal void SetupRoundKeys()
        {
            this.SetupDecRoundKeys();
        }

        private int ToInt(byte b0, byte b1, byte b2, byte b3)
        {
            return (b0 & 0xff) << 0x18 ^ (b1 & 0xff) << 0x10 ^ (b2 & 0xff) << 8 ^ b3 & 0xff;
        }

        private void ToByteArray(int i, byte[] b, int offset)
        {
            b[offset] = (byte)((uint)i >> 0x18);
            b[offset + 1] = (byte)((uint)i >> 0x10);
            b[offset + 2] = (byte)((uint)i >> 8);
            b[offset + 3] = (byte)((uint)i);
        }

        private int Badc(int t)
        {
            return unchecked((int)((t << 8) & 0xff00ff00)) ^ unchecked((int)(((uint)t >> 8) & 0x00ff00ff));
        }

        private int Cdab(int t)
        {
            return unchecked((int)(((t << 0x10) & 0xffff0000)) ^ unchecked((int)(((uint)t >> 0x10) & 0x0000ffff)));
        }

        private int Dcba(int t)
        {
            return unchecked((int)((t & 0x000000ff) << 0x18)) ^ unchecked((int)((t & 0x0000ff00) << 8)) ^ unchecked((int)((uint)(t & 0x00ff0000) >> 8)) ^ unchecked((int)((uint)(t & 0xff000000) >> 0x18));
        }

        private void DoCrypt(byte[] i, int ioffset, int[] rk, int nr, byte[] o, int ooffset)
        {
            int j = 0;
            int t0 = this.ToInt(i[0 + ioffset], i[1 + ioffset], i[2 + ioffset], i[3 + ioffset]);
            int t1 = this.ToInt(i[4 + ioffset], i[5 + ioffset], i[6 + ioffset], i[7 + ioffset]);
            int t2 = this.ToInt(i[8 + ioffset], i[9 + ioffset], i[10 + ioffset], i[11 + ioffset]);
            int t3 = this.ToInt(i[12 + ioffset], i[13 + ioffset], i[14 + ioffset], i[15 + ioffset]);

            for (int r = 1; r < nr / 2; r++)
            {
                t0 ^= rk[j++];
                t1 ^= rk[j++];
                t2 ^= rk[j++];
                t3 ^= rk[j++];

                t0 = this.TS1[((uint)t0 >> 0x18) & 0xff] ^ this.TS2[((uint)t0 >> 0x10) & 0xff] ^ this.TX1[((uint)t0 >> 8) & 0xff] ^ this.TX2[t0 & 0xff];
                t1 = this.TS1[((uint)t1 >> 0x18) & 0xff] ^ this.TS2[((uint)t1 >> 0x10) & 0xff] ^ this.TX1[((uint)t1 >> 8) & 0xff] ^ this.TX2[t1 & 0xff];
                t2 = this.TS1[((uint)t2 >> 0x18) & 0xff] ^ this.TS2[((uint)t2 >> 0x10) & 0xff] ^ this.TX1[((uint)t2 >> 8) & 0xff] ^ this.TX2[t2 & 0xff];
                t3 = this.TS1[((uint)t3 >> 0x18) & 0xff] ^ this.TS2[((uint)t3 >> 0x10) & 0xff] ^ this.TX1[((uint)t3 >> 8) & 0xff] ^ this.TX2[t3 & 0xff];

                t1 ^= t2;
                t2 ^= t3;
                t0 ^= t1;
                t3 ^= t1;
                t2 ^= t0;
                t1 ^= t2;

                t1 = this.Badc(t1);
                t2 = this.Cdab(t2);
                t3 = this.Dcba(t3);

                t1 ^= t2;
                t2 ^= t3;
                t0 ^= t1;
                t3 ^= t1;
                t2 ^= t0;
                t1 ^= t2;

                t0 ^= rk[j++];
                t1 ^= rk[j++];
                t2 ^= rk[j++];
                t3 ^= rk[j++];

                t0 = this.TX1[((uint)t0 >> 0x18) & 0xff] ^ this.TX2[((uint)t0 >> 0x10) & 0xff] ^ this.TS1[((uint)t0 >> 8) & 0xff] ^ this.TS2[t0 & 0xff];
                t1 = this.TX1[((uint)t1 >> 0x18) & 0xff] ^ this.TX2[((uint)t1 >> 0x10) & 0xff] ^ this.TS1[((uint)t1 >> 8) & 0xff] ^ this.TS2[t1 & 0xff];
                t2 = this.TX1[((uint)t2 >> 0x18) & 0xff] ^ this.TX2[((uint)t2 >> 0x10) & 0xff] ^ this.TS1[((uint)t2 >> 8) & 0xff] ^ this.TS2[t2 & 0xff];
                t3 = this.TX1[((uint)t3 >> 0x18) & 0xff] ^ this.TX2[((uint)t3 >> 0x10) & 0xff] ^ this.TS1[((uint)t3 >> 8) & 0xff] ^ this.TS2[t3 & 0xff];

                t1 ^= t2;
                t2 ^= t3;
                t0 ^= t1;
                t3 ^= t1;
                t2 ^= t0;
                t1 ^= t2;

                t3 = this.Badc(t3);
                t0 = this.Cdab(t0);
                t1 = this.Dcba(t1);

                t1 ^= t2;
                t2 ^= t3;
                t0 ^= t1;
                t3 ^= t1;
                t2 ^= t0;
                t1 ^= t2;
            }

            t0 ^= rk[j++];
            t1 ^= rk[j++];
            t2 ^= rk[j++];
            t3 ^= rk[j++];

            t0 = this.TS1[((uint)t0 >> 0x18) & 0xff] ^ this.TS2[((uint)t0 >> 0x10) & 0xff] ^ this.TX1[((uint)t0 >> 8) & 0xff] ^ this.TX2[t0 & 0xff];
            t1 = this.TS1[((uint)t1 >> 0x18) & 0xff] ^ this.TS2[((uint)t1 >> 0x10) & 0xff] ^ this.TX1[((uint)t1 >> 8) & 0xff] ^ this.TX2[t1 & 0xff];
            t2 = this.TS1[((uint)t2 >> 0x18) & 0xff] ^ this.TS2[((uint)t2 >> 0x10) & 0xff] ^ this.TX1[((uint)t2 >> 8) & 0xff] ^ this.TX2[t2 & 0xff];
            t3 = this.TS1[((uint)t3 >> 0x18) & 0xff] ^ this.TS2[((uint)t3 >> 0x10) & 0xff] ^ this.TX1[((uint)t3 >> 8) & 0xff] ^ this.TX2[t3 & 0xff];

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            t1 = this.Badc(t1);
            t2 = this.Cdab(t2);
            t3 = this.Dcba(t3);

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            t0 ^= rk[j++];
            t1 ^= rk[j++];
            t2 ^= rk[j++];
            t3 ^= rk[j++];

            o[0 + ooffset] = (byte)(X1[0xff & ((uint)t0 >> 0x18)] ^ ((uint)rk[j] >> 0x18));
            o[1 + ooffset] = (byte)(X2[0xff & ((uint)t0 >> 0x10)] ^ ((uint)rk[j] >> 0x10));
            o[2 + ooffset] = (byte)(S1[0xff & ((uint)t0 >> 8)] ^ ((uint)rk[j] >> 8));
            o[3 + ooffset] = (byte)(S2[0xff & (t0)] ^ (rk[j]));
            o[4 + ooffset] = (byte)(X1[0xff & ((uint)t1 >> 0x18)] ^ ((uint)rk[j + 1] >> 0x18));
            o[5 + ooffset] = (byte)(X2[0xff & ((uint)t1 >> 0x10)] ^ ((uint)rk[j + 1] >> 0x10));
            o[6 + ooffset] = (byte)(S1[0xff & ((uint)t1 >> 8)] ^ ((uint)rk[j + 1] >> 8));
            o[7 + ooffset] = (byte)(S2[0xff & (t1)] ^ (rk[j + 1]));
            o[8 + ooffset] = (byte)(X1[0xff & ((uint)t2 >> 0x18)] ^ ((uint)rk[j + 2] >> 0x18));
            o[9 + ooffset] = (byte)(X2[0xff & ((uint)t2 >> 0x10)] ^ ((uint)rk[j + 2] >> 0x10));
            o[10 + ooffset] = (byte)(S1[0xff & ((uint)t2 >> 8)] ^ ((uint)rk[j + 2] >> 8));
            o[11 + ooffset] = (byte)(S2[0xff & (t2)] ^ (rk[j + 2]));
            o[12 + ooffset] = (byte)(X1[0xff & ((uint)t3 >> 0x18)] ^ ((uint)rk[j + 3] >> 0x18));
            o[13 + ooffset] = (byte)(X2[0xff & ((uint)t3 >> 0x10)] ^ ((uint)rk[j + 3] >> 0x10));
            o[14 + ooffset] = (byte)(S1[0xff & ((uint)t3 >> 8)] ^ ((uint)rk[j + 3] >> 8));
            o[15 + ooffset] = (byte)(S2[0xff & (t3)] ^ (rk[j + 3]));
        }

        private void DoEncKeySetup(byte[] mk, int[] rk, int keyBits)
        {
            int t0;
            int t1;
            int t2;
            int t3;
            int q;
            int j = 0;

            int[] w0 = new int[4];
            int[] w1 = new int[4];
            int[] w2 = new int[4];
            int[] w3 = new int[4];

            w0[0] = this.ToInt(mk[0], mk[1], mk[2], mk[3]);
            w0[1] = this.ToInt(mk[4], mk[5], mk[6], mk[7]);
            w0[2] = this.ToInt(mk[8], mk[9], mk[10], mk[11]);
            w0[3] = this.ToInt(mk[12], mk[13], mk[14], mk[15]);

            q = (keyBits - 0x80) / 0x40;

            t0 = w0[0] ^ this.KRK[q][0];
            t1 = w0[1] ^ this.KRK[q][1];
            t2 = w0[2] ^ this.KRK[q][2];
            t3 = w0[3] ^ this.KRK[q][3];

            t0 = this.TS1[((uint)t0 >> 0x18) & 0xff] ^ this.TS2[((uint)t0 >> 0x10) & 0xff] ^ this.TX1[((uint)t0 >> 8) & 0xff] ^ this.TX2[t0 & 0xff];
            t1 = this.TS1[((uint)t1 >> 0x18) & 0xff] ^ this.TS2[((uint)t1 >> 0x10) & 0xff] ^ this.TX1[((uint)t1 >> 8) & 0xff] ^ this.TX2[t1 & 0xff];
            t2 = this.TS1[((uint)t2 >> 0x18) & 0xff] ^ this.TS2[((uint)t2 >> 0x10) & 0xff] ^ this.TX1[((uint)t2 >> 8) & 0xff] ^ this.TX2[t2 & 0xff];
            t3 = this.TS1[((uint)t3 >> 0x18) & 0xff] ^ this.TS2[((uint)t3 >> 0x10) & 0xff] ^ this.TX1[((uint)t3 >> 8) & 0xff] ^ this.TX2[t3 & 0xff];

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            t1 = this.Badc(t1);
            t2 = this.Cdab(t2);
            t3 = this.Dcba(t3);

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            if (keyBits > 128)
            {
                w1[0] = this.ToInt(mk[16], mk[17], mk[18], mk[19]);
                w1[1] = this.ToInt(mk[20], mk[21], mk[22], mk[23]);

                if (keyBits > 192)
                {
                    w1[2] = this.ToInt(mk[24], mk[25], mk[26], mk[27]);
                    w1[3] = this.ToInt(mk[28], mk[29], mk[30], mk[31]);
                }
                else
                {
                    w1[2] = w1[3] = 0;
                }
            }
            else
            {
                w1[0] = w1[1] = w1[2] = w1[3] = 0;
            }

            w1[0] ^= t0;
            w1[1] ^= t1;
            w1[2] ^= t2;
            w1[3] ^= t3;

            t0 = w1[0];
            t1 = w1[1];
            t2 = w1[2];
            t3 = w1[3];

            q = (q == 2) ? 0 : (q + 1);

            t0 ^= this.KRK[q][0];
            t1 ^= this.KRK[q][1];
            t2 ^= this.KRK[q][2];
            t3 ^= this.KRK[q][3];

            t0 = this.TX1[((uint)t0 >> 0x18) & 0xff] ^ this.TX2[((uint)t0 >> 0x10) & 0xff] ^ this.TS1[((uint)t0 >> 8) & 0xff] ^ this.TS2[t0 & 0xff];
            t1 = this.TX1[((uint)t1 >> 0x18) & 0xff] ^ this.TX2[((uint)t1 >> 0x10) & 0xff] ^ this.TS1[((uint)t1 >> 8) & 0xff] ^ this.TS2[t1 & 0xff];
            t2 = this.TX1[((uint)t2 >> 0x18) & 0xff] ^ this.TX2[((uint)t2 >> 0x10) & 0xff] ^ this.TS1[((uint)t2 >> 8) & 0xff] ^ this.TS2[t2 & 0xff];
            t3 = this.TX1[((uint)t3 >> 0x18) & 0xff] ^ this.TX2[((uint)t3 >> 0x10) & 0xff] ^ this.TS1[((uint)t3 >> 8) & 0xff] ^ this.TS2[t3 & 0xff];

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            t3 = this.Badc(t3);
            t0 = this.Cdab(t0);
            t1 = this.Dcba(t1);

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            t0 ^= w0[0];
            t1 ^= w0[1];
            t2 ^= w0[2];
            t3 ^= w0[3];

            w2[0] = t0;
            w2[1] = t1;
            w2[2] = t2;
            w2[3] = t3;

            q = (q == 2) ? 0 : (q + 1);

            t0 ^= this.KRK[q][0];
            t1 ^= this.KRK[q][1];
            t2 ^= this.KRK[q][2];
            t3 ^= this.KRK[q][3];

            t0 = this.TS1[((uint)t0 >> 0x18) & 0xff] ^ this.TS2[((uint)t0 >> 0x10) & 0xff] ^ this.TX1[((uint)t0 >> 8) & 0xff] ^ this.TX2[t0 & 0xff];
            t1 = this.TS1[((uint)t1 >> 0x18) & 0xff] ^ this.TS2[((uint)t1 >> 0x10) & 0xff] ^ this.TX1[((uint)t1 >> 8) & 0xff] ^ this.TX2[t1 & 0xff];
            t2 = this.TS1[((uint)t2 >> 0x18) & 0xff] ^ this.TS2[((uint)t2 >> 0x10) & 0xff] ^ this.TX1[((uint)t2 >> 8) & 0xff] ^ this.TX2[t2 & 0xff];
            t3 = this.TS1[((uint)t3 >> 0x18) & 0xff] ^ this.TS2[((uint)t3 >> 0x10) & 0xff] ^ this.TX1[((uint)t3 >> 8) & 0xff] ^ this.TX2[t3 & 0xff];

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            t1 = this.Badc(t1);
            t2 = this.Cdab(t2);
            t3 = this.Dcba(t3);

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            w3[0] = t0 ^ w1[0];
            w3[1] = t1 ^ w1[1];
            w3[2] = t2 ^ w1[2];
            w3[3] = t3 ^ w1[3];

            this.Gsrk(w0, w1, 19, rk, j); j += 4;
            this.Gsrk(w1, w2, 19, rk, j); j += 4;
            this.Gsrk(w2, w3, 19, rk, j); j += 4;
            this.Gsrk(w3, w0, 19, rk, j); j += 4;
            this.Gsrk(w0, w1, 31, rk, j); j += 4;
            this.Gsrk(w1, w2, 31, rk, j); j += 4;
            this.Gsrk(w2, w3, 31, rk, j); j += 4;
            this.Gsrk(w3, w0, 31, rk, j); j += 4;
            this.Gsrk(w0, w1, 67, rk, j); j += 4;
            this.Gsrk(w1, w2, 67, rk, j); j += 4;
            this.Gsrk(w2, w3, 67, rk, j); j += 4;
            this.Gsrk(w3, w0, 67, rk, j); j += 4;
            this.Gsrk(w0, w1, 97, rk, j); j += 4;

            if (keyBits > 0x80)
            {
                this.Gsrk(w1, w2, 97, rk, j); j += 4;
                this.Gsrk(w2, w3, 97, rk, j); j += 4;
            }
            if (keyBits > 0xc0)
            {
                this.Gsrk(w3, w0, 97, rk, j); j += 4;
                this.Gsrk(w0, w1, 109, rk, j);
            }
        }

        private void Gsrk(int[] x, int[] y, int rot, int[] rk, int offset)
        {
            int q = 4 - (rot / 32);
            int r = rot % 32;
            int s = 32 - r;
            rk[offset] = (x[0] ^ (int)((uint)y[q % 4] >> r)) ^ y[(q + 3) % 4] << s;
            rk[offset + 1] = (x[1] ^ (int)((uint)y[(q + 1) % 4] >> r)) ^ y[q % 4] << s;
            rk[offset + 2] = (x[2] ^ (int)((uint)y[(q + 2) % 4] >> r)) ^ y[(q + 1) % 4] << s;
            rk[offset + 3] = (x[3] ^ (int)((uint)y[(q + 3) % 4] >> r)) ^ y[(q + 2) % 4] << s;
        }

        internal void Encrypt(byte[] i, int ioffset, byte[] o, int ooffset)
        {
            if (this.keySize == 0)
            {
                throw new ArgumentException("keySize");
            }
            if (this.encRoundKeys == null)
            {
                if (this.masterKey == null)
                {
                    throw new ArgumentException("masterKey");
                }
                else
                {
                    this.SetupEncRoundKeys();
                }
            }
            this.DoCrypt(i, ioffset, this.encRoundKeys, this.numberOfRounds, o, ooffset);
        }

        internal byte[] Encrypt(byte[] i, int ioffset)
        {
            byte[] o = new byte[16];
            this.Encrypt(i, ioffset, o, 0);
            return o;
        }

        internal void Decrypt(byte[] i, int ioffset, byte[] o, int ooffset)
        {
            if (this.keySize == 0)
            {
                throw new ArgumentException("keySize");
            }
            if (this.decRoundKeys == null)
            {
                if (this.masterKey == null)
                {
                    throw new ArgumentException("masterKey");
                }
                else
                {
                    this.SetupDecRoundKeys();
                }
            }
            this.DoCrypt(i, ioffset, this.decRoundKeys, this.numberOfRounds, o, ooffset);
        }

        internal byte[] Decrypt(byte[] i, int ioffset)
        {
            byte[] o = new byte[16];
            this.Decrypt(i, ioffset, o, 0);
            return o;
        }

        /// <summary>
        /// Main bulk of the decryption key setup method.  Here we assume that
        /// the int array rk already contains the encryption round keys.
        /// @param mk the master key
        /// @param rk the array which contains the encryption round keys at the
        /// beginning of the method execution.  At the end of method execution
        /// this will hold the decryption round keys.
        /// @param keyBits the length of the master key
        /// @return
        /// </summary>
        /// <param name="mk"></param>
        /// <param name="rk"></param>
        /// <param name="keyBits"></param>
        private void DoDecKeySetup(byte[] mk, int[] rk, int keyBits)
        {
            int a = 0;
            int z;
            int[] t = new int[4];

            z = 32 + keyBits / 8;
            this.SwapBlocks(rk, 0, z);
            a += 4; z -= 4;

            for (; a < z; a += 4, z -= 4)
            {
                this.SwapAndDiffuse(rk, a, z, t);
            }
            this.Diff(rk, a, t, 0);

            rk[a] = t[0];
            rk[a + 1] = t[1];
            rk[a + 2] = t[2];
            rk[a + 3] = t[3];
        }

        private void Diff(int[] i, int offset1, int[] o, int offset2)
        {
            int t0, t1, t2, t3;

            t0 = this.M(i[offset1]);
            t1 = this.M(i[offset1 + 1]);
            t2 = this.M(i[offset1 + 2]);
            t3 = this.M(i[offset1 + 3]);
            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            t1 = this.Badc(t1);
            t2 = this.Cdab(t2);
            t3 = this.Dcba(t3);

            t1 ^= t2;
            t2 ^= t3;
            t0 ^= t1;
            t3 ^= t1;
            t2 ^= t0;
            t1 ^= t2;

            o[offset2] = t0;
            o[offset2 + 1] = t1;
            o[offset2 + 2] = t2;
            o[offset2 + 3] = t3;
        }

        private int M(int t)
        {
            return (int)(0x00010101 * (((uint)t >> 0x18) & 0xff)) ^ (int)(0x01000101 * (((uint)t >> 0x10) & 0xff)) ^ (int)(0x01010001 * (((uint)t >> 8) & 0xff)) ^ 0x01010100 * (t & 0xff);
        }

        private void SwapAndDiffuse(int[] arr, int offset1, int offset2, int[] tmp)
        {
            this.Diff(arr, offset1, tmp, 0);
            this.Diff(arr, offset2, arr, offset1);
            arr[offset2] = tmp[0];
            arr[offset2 + 1] = tmp[1];
            arr[offset2 + 2] = tmp[2];
            arr[offset2 + 3] = tmp[3];
        }

        private void SwapBlocks(int[] arr, int offset1, int offset2)
        {
            int t;

            for (int i = 0; i < 4; i++)
            {
                t = arr[offset1 + i];
                arr[offset1 + i] = arr[offset2 + i];
                arr[offset2 + i] = t;
            }
        }
    }
}