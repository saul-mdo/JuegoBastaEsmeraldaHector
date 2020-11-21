using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JuegoBasta
{
   public partial class Juego
    {
        public bool showRespuestasServidor
        {
            get
            {
                return servidor != null || (ConfirmacionCliente == true && ConfirmacionServer == true) ;
            }
        }


        public bool showRespuestasCliente
        {
            get
            {
                return cliente != null || (ConfirmacionCliente == true && ConfirmacionServer == true);
            }
        }
    }
}
