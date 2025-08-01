using Newtonsoft.Json;

namespace CHITSCHEME.Models
{
    public class RegisterUser
    {
        [JsonProperty("Firstname")]
        public string Firstname { get; set; }

        [JsonProperty("Email")]
        public string Email { get; set; }

        [JsonProperty("Phonenumber")]
        public string Phonenumber { get; set; }

    }
       
}
