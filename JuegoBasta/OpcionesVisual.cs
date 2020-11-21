using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JuegoBasta
{
   public partial class Juego
    {
        public bool showRespuestas1
        {
            get
            {
                return servidor != null || (RespuestaJugador1 != null && RespuestaJugador2 != null);
            }
        }


        public bool showRespuestas2
        {
            get
            {
                return cliente != null || (RespuestaJugador1 != null && RespuestaJugador2 != null);
            }
        }
    }
}
