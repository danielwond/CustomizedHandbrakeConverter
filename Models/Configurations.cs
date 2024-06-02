using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Handbrake.Models
{
    public class Configurations
    {
        [AutoIncrement, PrimaryKey]
        public int ID { get; set; }
        public string? Name { get; set; }
        public long Value { get; set; }
        public string? Option { get; set; }
    }
}
