using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WeatherBot.Models
{

    public class LocationModel
    {
        public Location2[] locations { get; set; }
    }

    public class Location2
    {
        public string pref_id { get; set; }
        public string city_id { get; set; }
        public string pref { get; set; }
        public string city { get; set; }
    }

}