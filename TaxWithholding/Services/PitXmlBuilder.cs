using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitXmlBuilder
{
    public static void Validate(string kind, PitData data)
    {
        var schema = PitSchemas.Get(kind);
        foreach (var field in schema.Fields)
        {
            var value = data.Get(field.Key);
            if (field.Required && value.Length == 0) throw new ArgumentException($"Vui lòng nhập {field.Label}.");
            if (value.Length > field.MaxLength) throw new ArgumentException($"{field.Label} tối đa {field.MaxLength} ký tự.");
            if (value.Length == 0) continue;
            if (field.Type == "date" && !DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) throw new ArgumentException($"{field.Label} không hợp lệ.");
            if (field.Type == "integer" && !int.TryParse(value,out _)) throw new ArgumentException($"{field.Label} phải là số nguyên.");
            if (field.Type == "money" && (!decimal.TryParse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var amount) || decimal.Abs(amount)>999999999999999.999999m || decimal.Round(amount,6)!=amount || (amount<0 && data.Get("TCCTU")!="2"))) throw new ArgumentException($"{field.Label} không hợp lệ (tối đa 15 số nguyên, 6 số lẻ).");
            if (field.Options != null && !field.Options.Any(o => o.Value==value)) throw new ArgumentException($"{field.Label} không thuộc danh mục.");
            if (field.Type=="email" && !System.Net.Mail.MailAddress.TryCreate(value,out _)) throw new ArgumentException($"{field.Label} không hợp lệ.");
        }
        var dateText=data.Get(kind==PitKinds.ErrorNotice?"NTBAO":"NLAP");
        var date=dateText==""&&kind==PitKinds.Certificate
            ? DateTime.UtcNow.AddHours(7).Date
            : DateTime.ParseExact(dateText,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        if (date.Date>DateTime.UtcNow.AddHours(7).Date) throw new ArgumentException("Ngày lập không được ở tương lai.");
        if (kind==PitKinds.Declaration)
        {
            if (data.Certificates.Count==0) throw new ArgumentException("Khai báo ít nhất một chứng thư số sử dụng.");
            if (data.Certificates.Count>999) throw new ArgumentException("Danh sách chứng thư số vượt giới hạn.");
            foreach(var certificate in data.Certificates)
                if (string.IsNullOrWhiteSpace(certificate.TTCHUC)||certificate.TTCHUC.Length>400||string.IsNullOrWhiteSpace(certificate.SERI)||certificate.SERI.Length>40
                    ||!DateTimeOffset.TryParse(certificate.TNGAY,out var from)||!DateTimeOffset.TryParse(certificate.DNGAY,out var to)||to<=from||certificate.HTHUC is <1 or >3)
                    throw new ArgumentException("Thông tin chứng thư số chưa hợp lệ: tổ chức cấp, serial, thời hạn và hình thức.");
        }
        if (kind==PitKinds.Certificate)
        {
            if (!Regex.IsMatch(data.Get("KHCTU"),$"^CT{date:yy}[A-Z]{{2}}$")) throw new ArgumentException("Ký hiệu phải là CT + hai số năm lập + hai chữ in hoa (ví dụ CT26AA).");
            if (data.Get("NNT_MST")=="" && data.Get("NNT_CCCDAN")=="") throw new ArgumentException("Cá nhân chưa có MST phải có CCCD, hộ chiếu hoặc số định danh.");
            var from = int.Parse(data.Get("TTHANG")); var to = int.Parse(data.Get("DTHANG")); var year = int.Parse(data.Get("NAM"));
            if (from<1||to>12||from>to||year<1900||year>date.Year) throw new ArgumentException("Kỳ trả thu nhập không hợp lệ.");
            if (data.Get("TCCTU")!="")
                foreach(var key in new[]{"LHCTLQUAN","KHMSCTCLQUAN","KHCTCLQUAN","SCTCLQUAN","NLCTCLQUAN"})
                    if(data.Get(key)=="") throw new ArgumentException("Nhập đầy đủ thông tin chứng từ bị thay thế/điều chỉnh.");
        }
        if (kind==PitKinds.ErrorNotice)
        {
            if(data.Get("LOAI")=="2" && (data.Get("SO")==""||data.Get("NTBCCQT")=="")) throw new ArgumentException("Giải trình phải có số và ngày thông báo của CQT.");
            var hasTaxCode=!string.IsNullOrWhiteSpace(data.Get("MST"));
            var hasBudgetCode=!string.IsNullOrWhiteSpace(data.Get("MDVQHNSACH"));
            if(hasTaxCode==hasBudgetCode) throw new ArgumentException("Nhập một trong hai: mã số thuế hoặc mã đơn vị quan hệ ngân sách.");
            if(data.Items.Count==0||data.Items.Count>9999) throw new ArgumentException("Chọn từ 1 đến 9999 chứng từ đã lập sai.");
            foreach(var item in data.Items)
                if(string.IsNullOrWhiteSpace(item.KHMSCTU)||item.KHMSCTU.Length>7||string.IsNullOrWhiteSpace(item.KHCTU)||item.KHCTU.Length>6||!Regex.IsMatch(item.SCTU,@"^\d{1,8}$")||long.Parse(item.SCTU)==0
                   || !DateTime.TryParseExact(item.NLAP,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var itemDate)||itemDate>date||string.IsNullOrWhiteSpace(item.LCTDT)||item.LCTDT.Length>5||item.LDO.Length>255)
                    throw new ArgumentException("Dòng chứng từ sai sót chưa hợp lệ: mẫu số, ký hiệu, số, ngày hoặc loại chứng từ.");
            if(data.Items.Select(i=>$"{i.KHMSCTU}|{i.KHCTU}|{i.SCTU}|{i.NLAP}").Distinct().Count()!=data.Items.Count) throw new ArgumentException("Danh sách chứng từ sai sót bị trùng.");
        }
    }

    public static string Build(PitDocument document)
    {
        var schema=PitSchemas.Get(document.KIND); var data=document.DATA;
        Validate(document.KIND,data);
        var payload=new XElement(schema.DataRoot,new XAttribute("Id",$"PIT-{document.DOCUMENT_ID}-v{document.DOC_VERSION}"));
        var common=document.KIND==PitKinds.ErrorNotice?payload:new XElement("TTChung");
        if(common!=payload) payload.Add(common);
        common.Add(new XElement("PBan","2.1.1"));
        if(document.KIND==PitKinds.Certificate)
            common.Add(new XElement("TCTu",schema.Title),new XElement("MSCTu",schema.Form));
        else common.Add(new XElement("MSo",schema.Form),new XElement("Ten",schema.Title));
        foreach(var field in schema.Fields)
        {
            if(field.Group=="Chứng từ liên quan"&&data.Get("TCCTU")=="")continue;
            var value=data.Get(field.Key);
            if(value=="")continue;
            if(field.Type=="money")value=decimal.Parse(value,CultureInfo.InvariantCulture).ToString("0.######",CultureInfo.InvariantCulture);
            Put(payload,field.Path!,value);
        }
        if(document.KIND==PitKinds.Certificate)
        {
            var number=new XElement("SCTu",document.DOC_NO?.ToString(CultureInfo.InvariantCulture)??"0");
            common.Element("KHCTu")!.AddAfterSelf(number);
        }
        if(document.KIND==PitKinds.Declaration)
        {
            payload.Add(new XElement("NDTKhai",
                new XElement("DTPHanh",new XElement("TCCNPHanh",1),new XElement("CQTPHanh",0)),
                new XElement("LHSDung",new XElement("CTTNCNhan",1),new XElement("CTKTTTMDTu",0),new XElement("BLTPLPKIn",0),new XElement("BLTPLPIn",0),new XElement("BLTTPLPhi",0)),
                new XElement("HTGDLCTDT",new XElement("CDLQCCQT",0),new XElement("CDLQTCTN",1),new XElement("CDLQTCTNUT",0),new XElement("CDLTTiep",0),new XElement("CDLBTHBLai",0)),
                new XElement("DSCTSSDung",data.Certificates.Select((cert,i)=>new XElement("CTS",new XElement("STT",i+1),new XElement("TTChuc",cert.TTCHUC.Trim()),new XElement("Seri",cert.SERI.Trim()),
                    new XElement("TNgay",XmlDateTime(cert.TNGAY)),new XElement("DNgay",XmlDateTime(cert.DNGAY)),new XElement("HThuc",cert.HTHUC))))));
        }
        if(document.KIND==PitKinds.ErrorNotice)
            payload.Add(new XElement("DSCTu",data.Items.Select((item,i)=>new XElement("CTu",new XElement("STT",i+1),new XElement("KHMSCTu",item.KHMSCTU),new XElement("KHCTu",item.KHCTU),new XElement("SCTu",item.SCTU),new XElement("NLap",item.NLAP),new XElement("LCTDT",item.LCTDT),string.IsNullOrWhiteSpace(item.LDO)?null:new XElement("LDo",item.LDO)))));
        return new XElement(schema.Root,payload,new XElement("DSCKS",new XElement(schema.Signer))).ToString(SaveOptions.DisableFormatting);
    }
    private static string XmlDateTime(string value)=>DateTimeOffset.Parse(value).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture);
    private static void Put(XElement root,string path,string value)
    {
        var parts=path.Split('/');var current=root;
        foreach(var part in parts[..^1]) {var next=current.Element(part);if(next==null){next=new XElement(part);current.Add(next);}current=next;}
        current.Add(new XElement(parts[^1],value));
    }
}
