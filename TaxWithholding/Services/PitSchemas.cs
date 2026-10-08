namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitSchemas
{
    private static PitOption[] Options(params string[] labels) => labels.Select((label,i) => new PitOption((i+1).ToString(),label)).ToArray();
    private static PitField F(string key,string label,string group,string path,bool required=true,int max=400,string type="text",PitOption[]? options=null,string? value=null) => new(key,label,group,type,required,max,path,options,value);
    public static PitSchema Get(string kind) => kind switch
    {
        PitKinds.Declaration => new("Tờ khai đăng ký sử dụng chứng từ khấu trừ TNCN","01/ĐKTĐ-CTĐT","TKhai","DLTKhai","NNT",new[] {
            F("HTHUC","Hình thức đăng ký","Tờ khai","TTChung/HThuc",max:1,type:"select",options:Options("Đăng ký mới","Thay đổi thông tin"),value:"1"),
            F("TNNT","Tên tổ chức, cá nhân trả thu nhập","Người nộp thuế","TTChung/TNNT"),
            F("MST","Mã số thuế","Người nộp thuế","TTChung/MST",max:14),
            F("CQTQLY","Cơ quan thuế quản lý","Người nộp thuế","TTChung/CQTQLy",max:100),
            F("MCQTQLY","Mã cơ quan thuế","Người nộp thuế","TTChung/MCQTQLy",max:5),
            F("NLHE","Người liên hệ","Liên hệ","TTChung/NLHe",max:50),
            F("DCLHE","Địa chỉ liên hệ","Liên hệ","TTChung/DCLHe"),
            F("DCTDTU","Email","Liên hệ","TTChung/DCTDTu",max:50,type:"email"),
            F("DTLHE","Điện thoại liên hệ","Liên hệ","TTChung/DTLHe",max:20),
            F("DDANH","Địa danh","Tờ khai","TTChung/DDanh",max:50),
            F("NLAP","Ngày lập","Tờ khai","TTChung/NLap",type:"date") }),
        PitKinds.Certificate => new("Chứng từ khấu trừ thuế thu nhập cá nhân","03/TNCN","CTu","DLCTu","TCTTNhap",new[] {
            F("KHCTU","Ký hiệu chứng từ (CT26AA)","Chứng từ","TTChung/KHCTu",max:6),
            F("NLAP","Ngày lập khi ký","Chứng từ","TTChung/NLap",false,type:"date"),
            F("TCCTU","Tính chất","Chứng từ","TTChung/TTCTLQuan/TCCTu",false,1,"select",new[]{new PitOption("","Chứng từ gốc"),new PitOption("1","Thay thế"),new PitOption("2","Điều chỉnh chứng từ đã điều chỉnh theo NĐ 70")}),
            F("LHCTLQUAN","Loại chứng từ liên quan","Chứng từ liên quan","TTChung/TTCTLQuan/LHCTLQuan",false,1,"select",new[]{new PitOption("1","Theo NĐ 70"),new PitOption("7","Theo NĐ 123"),new PitOption("8","Theo NĐ 254")}),
            F("KHMSCTCLQUAN","Mẫu số chứng từ liên quan","Chứng từ liên quan","TTChung/TTCTLQuan/KHMSCTCLQuan",false,11),
            F("KHCTCLQUAN","Ký hiệu chứng từ liên quan","Chứng từ liên quan","TTChung/TTCTLQuan/KHCTCLQuan",false,9),
            F("SCTCLQUAN","Số chứng từ liên quan","Chứng từ liên quan","TTChung/TTCTLQuan/SCTCLQuan",false,8),
            F("NLCTCLQUAN","Ngày chứng từ liên quan","Chứng từ liên quan","TTChung/TTCTLQuan/NLCTCLQuan",false,type:"date"),
            F("GCHU","Ghi chú liên quan","Chứng từ liên quan","TTChung/TTCTLQuan/GChu",false,255),
            F("TCTTNHAP_TEN","Tên tổ chức, cá nhân trả thu nhập","Tổ chức trả thu nhập","NDCTu/TCTTNhap/Ten"),
            F("TCTTNHAP_MST","Mã số thuế tổ chức","Tổ chức trả thu nhập","NDCTu/TCTTNhap/MST",max:14),
            F("TCTTNHAP_DCHI","Địa chỉ tổ chức","Tổ chức trả thu nhập","NDCTu/TCTTNhap/DChi"),
            F("TCTTNHAP_SDTHOAI","Điện thoại tổ chức","Tổ chức trả thu nhập","NDCTu/TCTTNhap/SDThoai",false,20),
            F("NNT_TEN","Họ tên người nhận thu nhập","Người nhận thu nhập","NDCTu/NNT/Ten"),
            F("NNT_MST","Mã số thuế cá nhân","Người nhận thu nhập","NDCTu/NNT/MST",false,14),
            F("NNT_DCHI","Địa chỉ cá nhân","Người nhận thu nhập","NDCTu/NNT/DChi"),
            F("NNT_QTICH","Quốc tịch","Người nhận thu nhập","NDCTu/NNT/QTich",false),
            F("NNT_CNCTRU","Tình trạng cư trú","Người nhận thu nhập","NDCTu/NNT/CNCTru",true,1,"option",new[]{new PitOption("1","Cư trú"),new PitOption("0","Không cư trú")},"1"),
            F("NNT_CCCDAN","CCCD / hộ chiếu / số định danh","Người nhận thu nhập","NDCTu/NNT/CCCDan",false,20),
            F("NNT_SDTHOAI","Điện thoại cá nhân","Người nhận thu nhập","NDCTu/NNT/SDThoai",max:20),
            F("NNT_DCTDTU","Email cá nhân","Người nhận thu nhập","NDCTu/NNT/DCTDTu",false,50,"email"),
            F("NNT_GCHU","Ghi chú","Người nhận thu nhập","NDCTu/NNT/GChu",false,255),
            F("KTNHAP","Khoản thu nhập","Thu nhập và thuế","NDCTu/TTNCNKTru/KTNhap",true,250),
            F("TTHANG","Từ tháng","Thu nhập và thuế","NDCTu/TTNCNKTru/TThang",type:"integer"),
            F("DTHANG","Đến tháng","Thu nhập và thuế","NDCTu/TTNCNKTru/DThang",type:"integer"),
            F("NAM","Năm trả thu nhập","Thu nhập và thuế","NDCTu/TTNCNKTru/Nam",type:"integer"),
            F("BHIEM","Bảo hiểm bắt buộc","Thu nhập và thuế","NDCTu/TTNCNKTru/BHiem",type:"money",value:"0"),
            F("TTHIEN","Từ thiện, nhân đạo, khuyến học","Thu nhập và thuế","NDCTu/TTNCNKTru/TThien",type:"money",value:"0"),
            F("TTNCTHUE","Tổng thu nhập chịu thuế","Thu nhập và thuế","NDCTu/TTNCNKTru/TTNCThue",type:"money"),
            F("TTNTTHUE","Tổng thu nhập tính thuế","Thu nhập và thuế","NDCTu/TTNCNKTru/TTNTThue",type:"money"),
            F("STHUE","Số thuế TNCN đã khấu trừ","Thu nhập và thuế","NDCTu/TTNCNKTru/SThue",type:"money") }),
        PitKinds.ErrorNotice => new("Thông báo chứng từ điện tử đã lập sai","04/SS-CTĐT","TBao","DLTBao","NNT",new[] {
            F("LOAI","Loại thông báo","Thông báo","Loai",true,1,"select",Options("NNT thông báo","Giải trình theo thông báo của CQT"),"1"),
            F("SO","Số thông báo của CQT","Thông báo","So",false,30),
            F("NTBCCQT","Ngày thông báo của CQT","Thông báo","NTBCCQT",false,type:"date"),
            F("MCQT","Mã cơ quan thuế","Cơ quan thuế","MCQT",max:5),
            F("TCQT","Tên cơ quan thuế","Cơ quan thuế","TCQT",max:100),
            F("TNNT","Tên tổ chức, cá nhân trả thu nhập","Người nộp thuế","TNNT"),
            F("MST","Mã số thuế","Người nộp thuế","MST",false,14),
            F("MDVQHNSACH","Mã đơn vị quan hệ ngân sách","Người nộp thuế","MDVQHNSach",false,7),
            F("DDANH","Địa danh","Thông báo","DDanh",max:50),
            F("NTBAO","Ngày thông báo","Thông báo","NTBao",type:"date") }),
        _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
    };
}
