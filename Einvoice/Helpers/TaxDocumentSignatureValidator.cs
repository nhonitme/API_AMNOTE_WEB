using System.Security.Cryptography.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services;

// Shared verification for taxpayer-signed registration, certificate and error-notice XML.
public static class TaxDocumentSignatureValidator
{
    public static string Validate(string expected,string signed,string signerName)
    {
        if(Encoding.UTF8.GetByteCount(signed)>2*1024*1024)throw new ArgumentException("XML vượt giới hạn 2 MB.");
        var document=new XmlDocument{PreserveWhitespace=true,XmlResolver=null};document.LoadXml(signed);
        var root=XDocument.Parse(signed).Root??throw new ArgumentException("XML không hợp lệ.");
        XNamespace ds=SignedXml.XmlDsigNamespaceUrl;XNamespace xades="http://uri.etsi.org/01903/v1.3.2#";
        var container=root.Element("DSCKS")?.Element(signerName)??throw new ArgumentException("Thiếu vị trí chữ ký.");
        var signature=container.Elements(ds+"Signature").SingleOrDefault()??throw new ArgumentException("Thiếu chữ ký số.");
        var ids=document.SelectNodes("//*[@Id]")!.Cast<XmlElement>().Select(e=>e.GetAttribute("Id")).ToArray();
        if(ids.Distinct().Count()!=ids.Length)throw new ArgumentException("XML có Id trùng nhau.");
        var dataId=(string?)root.Elements().First().Attribute("Id");
        var properties=signature.Descendants(xades+"SignedProperties").SingleOrDefault();
        var references=signature.Element(ds+"SignedInfo")!.Elements(ds+"Reference").Select(e=>(string?)e.Attribute("URI")).ToArray();
        if(string.IsNullOrWhiteSpace(dataId)||properties==null||references.Length!=2||!references.Contains("#"+dataId)||!references.Contains("#"+(string?)properties.Attribute("Id")))throw new ArgumentException("Chữ ký phải bao gồm dữ liệu và SignedProperties.");
        var serial=GetVerifiedCertificateSerial(document,signerName);
        container.RemoveNodes();
        if(!XNode.DeepEquals(root,XDocument.Parse(expected).Root))throw new ArgumentException("Nội dung XML khác dữ liệu đã lưu. Tạo lại dữ liệu ký.");
        return serial;
    }

    public static string GetVerifiedCertificateSerial(string signedXml,string signerName)
    {
        if(Encoding.UTF8.GetByteCount(signedXml)>2*1024*1024)throw new ArgumentException("XML vượt giới hạn 2 MB.");
        var document=new XmlDocument{PreserveWhitespace=true,XmlResolver=null};
        document.LoadXml(signedXml);
        return GetVerifiedCertificateSerial(document,signerName);
    }

    public static string GetVerifiedCertificateSerialAtXPath(string signedXml,string signatureXPath)
    {
        if(Encoding.UTF8.GetByteCount(signedXml)>2*1024*1024)throw new ArgumentException("XML vượt giới hạn 2 MB.");
        var document=new XmlDocument{PreserveWhitespace=true,XmlResolver=null};
        document.LoadXml(signedXml);
        var signature=document.DocumentElement?.SelectSingleNode(signatureXPath) as XmlElement
            ??throw new ArgumentException("Thiếu chữ ký số.");
        return GetVerifiedCertificateSerial(document,signature);
    }

    private static string GetVerifiedCertificateSerial(XmlDocument document,string signerName)
    {
        var signature=document.DocumentElement?.SelectSingleNode($"DSCKS/{signerName}/*[local-name()='Signature']") as XmlElement
            ??throw new ArgumentException("Thiếu chữ ký số.");
        return GetVerifiedCertificateSerial(document,signature);
    }

    private static string GetVerifiedCertificateSerial(XmlDocument document,XmlElement signature)
    {
        var certificateNode=signature.SelectSingleNode(".//*[local-name()='X509Certificate']");
        if(string.IsNullOrWhiteSpace(certificateNode?.InnerText))
            throw new ArgumentException("Chữ ký không chứa chứng thư số.");
        X509Certificate2 certificate;
        try { certificate=new X509Certificate2(Convert.FromBase64String(certificateNode.InnerText.Trim())); }
        catch { throw new ArgumentException("Chứng thư số trong chữ ký không hợp lệ."); }
        using(certificate)
        {
            var verifier=new SignedXml(document);
            verifier.LoadXml(signature);
            if(!verifier.CheckSignature(certificate,true))throw new ArgumentException("Chữ ký số không hợp lệ.");
            var serial=NormalizeSerial(certificate.SerialNumber);
            if(serial.Length==0)throw new ArgumentException("Chứng thư số không có serial hợp lệ.");
            return serial;
        }
    }

    public static string NormalizeSerial(string? value)
        =>new((value??"").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
