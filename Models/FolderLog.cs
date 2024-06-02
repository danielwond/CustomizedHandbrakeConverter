using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Handbrake.Models
{
    public class FolderLog
    {
        [AutoIncrement, PrimaryKey]
        public int ID { get; set; }
        public string? Path { get; set; }
        public string date { get; set; }
    }
}
