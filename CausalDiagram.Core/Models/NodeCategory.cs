using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CausalDiagram.Core.Models
{
    public enum NodeCategory
    {
        Системные = 0,
        Подсистемные = 1,
        Компонент = 2,
        Процесс = 3,
        Человек = 4
    }
}
