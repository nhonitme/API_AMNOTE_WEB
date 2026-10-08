namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceSettingMapper
    {
        public static EInvoiceDecimalSettingDto ToDecimalDto(EInvoiceDecimalSetting entity)
        {
            return new EInvoiceDecimalSettingDto
            {
                SETTING_ID = entity.SETTING_ID,
                COMPANY_CD = entity.COMPANY_CD,
                XSL_ID = entity.XSL_ID,
                APPLY_TARGET = entity.APPLY_TARGET,
                FIELD_SCOPE = entity.FIELD_SCOPE,
                FIELD_NAME = entity.FIELD_NAME,
                LABEL_TEXT = entity.LABEL_TEXT,
                CAPTION = entity.CAPTION,
                CURRENCY_SCOPE = entity.CURRENCY_SCOPE,
                DECIMAL_SCALE = entity.DECIMAL_SCALE,
                ROUND_MODE = entity.ROUND_MODE,
                IS_ACTIVE = entity.IS_ACTIVE,
                SORT_ORDER = entity.SORT_ORDER,
                NOTE = entity.NOTE,
            };
        }

        public static EInvoiceDecimalSetting ToDecimalEntity(EInvoiceDecimalSettingSaveRequest request, string companyCd)
        {
            return new EInvoiceDecimalSetting
            {
                SETTING_ID = request.SETTING_ID,
                COMPANY_CD = companyCd,
                XSL_ID = request.XSL_ID,
                APPLY_TARGET = request.APPLY_TARGET,
                FIELD_SCOPE = request.FIELD_SCOPE,
                FIELD_NAME = request.FIELD_NAME,
                LABEL_TEXT = request.LABEL_TEXT,
                CAPTION = request.CAPTION,
                CURRENCY_SCOPE = request.CURRENCY_SCOPE,
                DECIMAL_SCALE = request.DECIMAL_SCALE,
                ROUND_MODE = request.ROUND_MODE,
                IS_ACTIVE = request.IS_ACTIVE,
                SORT_ORDER = request.SORT_ORDER,
                NOTE = request.NOTE
            };
        }

        public static EInvoiceUserSettingDto ToUserSettingDto(EInvoiceUserSetting entity)
        {
            return new EInvoiceUserSettingDto
            {
                SETTING_ID = entity.SETTING_ID,
                COMPANY_CD = entity.COMPANY_CD,
                USER_ID = entity.USER_ID,
                SETTING_KEY = entity.SETTING_KEY,
                SETTING_VALUE = entity.SETTING_VALUE,
                VALUE_TYPE = entity.VALUE_TYPE,
                ISDEL = entity.ISDEL,
            };
        }

        public static EInvoiceUserSetting ToUserSettingEntity(EInvoiceUserSettingSaveRequest request)
        {
            return new EInvoiceUserSetting
            {
                SETTING_ID = request.SETTING_ID,
                COMPANY_CD = request.COMPANY_CD ?? string.Empty,
                USER_ID = request.USER_ID ?? string.Empty,
                SETTING_KEY = request.SETTING_KEY,
                SETTING_VALUE = request.SETTING_VALUE,
                VALUE_TYPE = request.VALUE_TYPE,
                ISDEL = request.ISDEL
            };
        }

        public static EInvoiceAdminSettingDto ToAdminSettingDto(EInvoiceAdminSetting entity)
        {
            return new EInvoiceAdminSettingDto
            {
                SETTING_ID = entity.SETTING_ID,
                COMPANY_CD = entity.COMPANY_CD,
                USER_ID = entity.USER_ID,
                SETTING_TYPE = entity.SETTING_TYPE,
                SETTING_KEY = entity.SETTING_KEY,
                SETTING_VALUE = entity.SETTING_VALUE,
                VALUE_TYPE = entity.VALUE_TYPE,
                ISDEL = entity.ISDEL,
            };
        }
    }
}
