using System.Text.Json.Serialization;

namespace CHITSCHEME.Models
{
    public class ChitSchemeModel
    {
        public List<SchemeList> SchemeDetails { get; set; }
    }



    public class SchemeList
    {
        public string CusCode { get; set; }
        public string SchemeCode { get; set; }
        public string Amount { get; set; }
        public string FDUE { get; set; }
        public string TotalAmt { get; set; }
        public string CompCode { get; set; }
        [JsonPropertyName("weight")]
        public string Weight { get; set; }

    }
}
