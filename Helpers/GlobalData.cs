namespace API_AMNOTE_WEB.Helpers
{
    public static class GlobalData
    {
        //Database
        public static string serverAdress = "44A33CF6F9EA3856AE350547D4F6F4A4"; 
        public static string serverAdressAPI = "E67262C7C40CE5CF3880F4812856B97C";
        public static string dBConnectPort = "70ECD28A7D0DF81B3B09A3668F558263";
        public static string dBConnectID = "9A79FEE344001A5F1EBD758CF51F89D2";
        public static string dBConnectPW = "DFCA568D94D1C9BCD0103D161464703D";

        public static string managerDB = "EE4C436071E998FA78626961C677E663";

        public static string getCurrentServerAdress()
        {
            string str = serverAdress;
            AriaSecurity.AriaProvider ariase = new AriaSecurity.AriaProvider();

            str = ariase.DecryptFromString(str).Replace("\0", "").Trim();

            return str;
        }

        public static string getCurrentPort()
        {
            string str = dBConnectPort;
            AriaSecurity.AriaProvider ariase = new AriaSecurity.AriaProvider();

            str = ariase.DecryptFromString(str).Replace("\0", "").Trim();

            return str;
        }

        public static string getCurrentID()
        {
            string str = dBConnectID;
            AriaSecurity.AriaProvider ariase = new AriaSecurity.AriaProvider();

            str = ariase.DecryptFromString(str).Replace("\0", "").Trim();

            return str;
        }

        public static string getCurrentPW()
        {
            string str = dBConnectPW;
            AriaSecurity.AriaProvider ariase = new AriaSecurity.AriaProvider();

            str = ariase.DecryptFromString(str).Replace("\0", "").Trim();

            return str;
        }

    }
}
