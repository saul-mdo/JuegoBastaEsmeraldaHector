using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace JuegoBasta
{
    public enum Comando { NombreEnviado, LetraEnviada, JugadaEnviada }
    public class Juego : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void ActualizarValor(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void CambiarMensaje(string msj)
        {
            currentDispatcher.Invoke(new Action(() =>
            {

                Mensaje = msj;
                ActualizarValor();
            }));
        }


        public int pAnimal1 { get; set; }
        public int pAnimal2 { get; set; }

        public int pNombre1 { get; set; }
        public int pNombre2 { get; set; }

        public int pLugar1 { get; set; }
        public int pLugar2 { get; set; }

        public int pColor1 { get; set; }
        public int pColor2 { get; set; }
        public int pComida1 { get; set; }
        public int pComida2 { get; set; }

        public string Jugador1 { get; set; } = "User24";
        public string Jugador2 { get; set; }
        public string IP { get; set; } = "localhost";
        public string Mensaje { get; set; }
        public bool MainVisible { get; set; } = true;
        public ICommand IniciarCommand { get; set; }
        public ICommand JugarCommand { get; set; }
        public int PuntajeJugador1 { get; set; } = 0 ;
        public int PuntajeJugador2 { get; set; } = 0 ;
        Random r = new Random();


        public char Letra { get; set; }

        public Respuestas RespuestaJugador1 { get; set; } = new Respuestas();
        public Respuestas RespuestaJugador2 { get; set; } = new Respuestas();

        public bool PuedeJugarCliente { get; set; }
        public bool PuedeJugarServidor { get; set; }




        HttpListener servidor;
        ClientWebSocket cliente;
        Dispatcher currentDispatcher;
        WebSocket webSocket;

        public Juego()
        {
            currentDispatcher = Dispatcher.CurrentDispatcher;
            IniciarCommand = new RelayCommand<bool>(IniciarPartida);
            JugarCommand = new RelayCommand<Respuestas>(Jugar);
        }
        public char ElegirLetra()
        {
            return (char)r.Next('a', 'z'); 
        }

        // SI CREA PARTIDA SE INICIA COMO SERVIDOR
        public void CrearPartida()
        {
            servidor = new HttpListener();
            servidor.Prefixes.Add("http://*:8080/basta/");
            servidor.Start();
            servidor.BeginGetContext(OnContext, null);

            Mensaje = "Esperando adversario...";
            ActualizarValor();
        }
        // SI SE UNE A UNA PARTIDA INICIA COMO CLIENTE
        public async Task ConectarPartida()
        {
            cliente = new ClientWebSocket();
            await cliente.ConnectAsync(new Uri($"ws://{IP}:8080/basta/"), CancellationToken.None);
            webSocket = cliente;
            // EL CLIENTE RECIBE EL NOMBRE DEL SERVIDOR
            RecibirComando();
        }
        // CUANDO RECIBE UNA CONEXIÓN CON EL CLIENTE
        private async void OnContext(IAsyncResult ar)
        {
            var context = servidor.EndGetContext(ar);

            if (context.Request.IsWebSocketRequest)
            {
                var listener = await context.AcceptWebSocketAsync(null);
                webSocket = listener.WebSocket;
                CambiarMensaje("Conexión exitosa. Esperando información del contrincante.");
                // EL SERVIDOR ENVÍA SU NOMBRE
                EnviarComando(new DatoEnviado { Comando = Comando.NombreEnviado, Nombre = Jugador1 });
                // EL SERVIDOR RECIBE EL COMANDO, QUE SERÍA EL NOMBRE DEL CLIENTE.
                RecibirComando();

                
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                servidor.BeginGetContext(OnContext, null);
            }
        }

        LobbyWindow lobby;
        // CUANDO YA SE INICIÓ, PERO AÚN NO SE CONECTAN EL SERVIDOR Y EL CLIENTE
        private async void IniciarPartida(bool tipoP)
        {
            try
            {
                MainVisible = false;
                lobby = new LobbyWindow();
                lobby.Closing += Lobby_Closing;
                lobby.DataContext = this;
                lobby.Show();
                ActualizarValor();

                // SI ES SERVIDOR, CREA LA PARTIDA
                if (tipoP == true)
                {
                    CrearPartida();
                }
                else // SI ES CLIENTE
                {
                    Jugador2 = Jugador1;
                    Jugador1 = null;
                    Mensaje = "Intentando conectar con el contrincante en " + IP;
                    ActualizarValor("Mensaje");
                    await ConectarPartida();
                }

            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
                ActualizarValor();
            }

        }

        private void Lobby_Closing(object sender, CancelEventArgs e)
        {
            MainVisible = true;
            ActualizarValor("MainVisible");
            if (servidor != null)
            {
                servidor.Stop();
                servidor = null;
            }
        }

        public async void EnviarComando(DatoEnviado datos)
        {
            byte[] buffer;
            var json = JsonConvert.SerializeObject(datos);
            buffer = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        JuegoBastaWindow ventanaJuego;
        public string BotonCliente { get; set; } = "Hidden";
        public string BotonServer { get; set; } = "Hidden";
        private async void RecibirComando()
        {
            try
            {
                byte[] buffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open)
                {
                    var resultado = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (resultado.MessageType == WebSocketMessageType.Close)
                    {
                        ventanaJuego.Close();
                        return;
                    }

                    string datosrecibidos = Encoding.UTF8.GetString(buffer, 0, resultado.Count);
                    var comandorecibido = JsonConvert.DeserializeObject<DatoEnviado>(datosrecibidos);

                    // YA ESTÁN CONECTADOS EL CLIENTE Y EL SERVIDOR. SE ABRE LA VENTANA DEL JUEGO.

                    if (cliente != null) // ES CLIENTE
                    {
                        switch (comandorecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador1 = (string)comandorecibido.Nombre;
                                CambiarMensaje("Se ha conectado con el jugador " + Jugador1);
                                _ = currentDispatcher.BeginInvoke(new Action(() =>
                                {
                                    // EL CLIENTE ENVÍA EL NOMBRE.
                                    EnviarComando(new DatoEnviado { Comando = Comando.NombreEnviado, Nombre = Jugador2 });
                                    lobby.Hide();

                                    // SE RECIBE LA LETRA
                                    //RecibirComando();
                                    BotonCliente = "Visible";
                                    ventanaJuego = new JuegoBastaWindow();
                                    ventanaJuego.Title = "Cliente";
                                    ventanaJuego.DataContext = this;

                                    PuedeJugarCliente = true;
                                    PuedeJugarServidor = false;
                                    CambiarMensaje("Inicie juego");
                                    ventanaJuego.ShowDialog();
                                    lobby.Show();

                                }));

                                break;

                            case Comando.JugadaEnviada:
                                currentDispatcher.Invoke(new Action(() =>
                                {
                                    RespuestaJugador1 = (Respuestas)comandorecibido.DatoRespuestas;
                                    CambiarMensaje("Respuestas recibidas");
                                    ActualizarValor();
                                }));
                                _ = ValidarRespuestas();
                                break;



                            case Comando.LetraEnviada:
                                // LETRA NO TOMA EL VALOR DE DATO
                                Letra = (char)comandorecibido.LetraRandom;
                                ActualizarValor();
                                break;
                        }
                    }
                    else // SERVIDOR
                    {
                        switch (comandorecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador2 = (string)comandorecibido.Nombre;
                                CambiarMensaje("Se ha conectado con el jugador " + Jugador2);

                                _ = currentDispatcher.BeginInvoke(new Action(() =>
                                {
                                    lobby.Hide();
                                    BotonServer = "Visible";
                                    JuegoBastaWindow ventanaJuego = new JuegoBastaWindow();
                                    ventanaJuego.Title = "Servidor";
                                    ventanaJuego.DataContext = this;

                                    Letra = ElegirLetra();
                                    EnviarComando(new DatoEnviado { Comando = Comando.LetraEnviada, LetraRandom = Letra });

                                    PuedeJugarCliente = false;
                                    PuedeJugarServidor = true;
                                    CambiarMensaje("Inicie juego");
                                    ventanaJuego.ShowDialog();
                                    lobby.Show();
                                }));
                                
                                break;

                            case Comando.JugadaEnviada:
                                currentDispatcher.Invoke(new Action(() =>
                                {
                                    RespuestaJugador2 = (Respuestas)comandorecibido.DatoRespuestas;
                                    CambiarMensaje("Respuestas recibidas");
                                    ActualizarValor();
                                }));
                                _ = ValidarRespuestas();
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (webSocket.State == WebSocketState.Aborted)
                {
                    //ventanaJuego.Close();
                    //lobby.Close();
                    MainVisible = true;
                    ActualizarValor("MainVisible");
                }
                else
                {
                    CambiarMensaje(ex.Message);
                }
            }

        }
      
        private void Jugar(Respuestas obj)
        {
            if (cliente != null)//Esta jugando el cliente.
            {
                RespuestaJugador2 = obj;
                
                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, DatoRespuestas = RespuestaJugador2 });
                ActualizarValor();                
                CambiarMensaje("¡Respuesta enviada!");
                
                PuedeJugarCliente = false;
            }
            else //Juega un servidor
            {
                RespuestaJugador1 = obj;
                
                EnviarComando(new DatoEnviado { Comando = Comando.JugadaEnviada, DatoRespuestas = RespuestaJugador1 });
                ActualizarValor();                
                CambiarMensaje("¡Respuesta enviada!");
                
                PuedeJugarServidor = false;
            }
            
            
            _ = ValidarRespuestas();
        }

        async Task ValidarRespuestas()
        {
            int puntaje1 = 0;
            int puntaje2 = 0;
            if((RespuestaJugador1.Color!=null || RespuestaJugador1.Comida!=null || RespuestaJugador1.Animal!=null || RespuestaJugador1.Lugar!=null || RespuestaJugador1.Nombre!=null)
                &&
                (RespuestaJugador2.Color != null || RespuestaJugador2.Comida != null || RespuestaJugador2.Animal != null || RespuestaJugador2.Lugar != null || RespuestaJugador2.Nombre != null))
            {
                if(RespuestaJugador1.Nombre.ToUpper()!=RespuestaJugador2.Nombre.ToUpper())
                {
                    pNombre1 = pNombre1+100;
                    pNombre2 = pNombre2+100;
                }
                 if(RespuestaJugador1.Nombre.ToUpper() == RespuestaJugador2.Nombre.ToUpper())
                {
                    pNombre1 = pNombre1+50;
                    pNombre2 = pNombre2+50;
                }
                 if(!RespuestaJugador1.Nombre.StartsWith(Letra.ToString())|| RespuestaJugador1.Nombre == null)
                {
                    pNombre1 = pNombre1;
                }
                if (!RespuestaJugador2.Nombre.StartsWith(Letra.ToString()) || RespuestaJugador2.Nombre == null)
                {
                    pNombre2 = pNombre2;
                }
               // await Task.Delay(200);

                //Validaciones de comida
                if (RespuestaJugador1.Comida.ToUpper() != RespuestaJugador2.Comida.ToUpper())
                {
                    pComida1 = pComida1+100;
                    pComida2 = pComida2+100;
                }
                 if (RespuestaJugador1.Comida.ToUpper() == RespuestaJugador2.Comida.ToUpper())
                {
                    pComida1 = pComida1+50;
                    pComida2 = pComida2+50;
                }
                 if (!RespuestaJugador1.Comida.StartsWith(Letra.ToString()) || RespuestaJugador1.Comida == null)
                {
                    pComida1 = pComida1;
                }
                if (!RespuestaJugador2.Comida.StartsWith(Letra.ToString()) || RespuestaJugador2.Comida == null)
                {
                    pComida2 = pComida2;
                }

                //Validaciones de color
                if (RespuestaJugador1.Color.ToUpper() != RespuestaJugador2.Color.ToUpper())
                {
                    pColor1 = pColor1+100;
                    pColor2 = pColor2+100;
                }
                 if (RespuestaJugador1.Color.ToUpper() == RespuestaJugador2.Color.ToUpper())
                {
                    pColor1 = pColor1+50;
                    pColor2 = pColor2+50;
                }
                 if (!RespuestaJugador1.Color.StartsWith(Letra.ToString()) || RespuestaJugador1.Color == null)
                {
                    pColor1 = pColor1;
                }
                if (!RespuestaJugador2.Color.StartsWith(Letra.ToString()) || RespuestaJugador2.Color == null)
                {
                    pColor2 = pColor2;
                }
                //Validaciones de animal
                if (RespuestaJugador1.Animal.ToUpper() != RespuestaJugador2.Animal.ToUpper())
                {
                    pAnimal1 = pAnimal1+100;
                    pAnimal2 = pAnimal2+100;
                }
                 if (RespuestaJugador1.Animal.ToUpper() == RespuestaJugador2.Animal.ToUpper())
                {
                    pAnimal1 = pAnimal1+50;
                    pAnimal2 = pAnimal2+50;
                }
                 if (!RespuestaJugador1.Animal.StartsWith(Letra.ToString()) || RespuestaJugador1.Animal == null)
                {
                    pAnimal1 = pAnimal1;
                }
                if (!RespuestaJugador2.Animal.StartsWith(Letra.ToString()) || RespuestaJugador2.Animal == null)
                {
                    pAnimal2 = pAnimal2;
                }
                //Validaciones de lugar
                if (RespuestaJugador1.Lugar.ToUpper() != RespuestaJugador2.Lugar.ToUpper())
                {
                    pLugar1 = pLugar1+100;
                    pLugar2 = pLugar2+100;
                }
                 if (RespuestaJugador1.Animal.ToUpper() == RespuestaJugador2.Animal.ToUpper())
                {
                    pLugar1 = pLugar1+50;
                    pLugar2 = pLugar2+50;
                }
                 if (!RespuestaJugador1.Animal.StartsWith(Letra.ToString()) || RespuestaJugador1.Animal == null)
                {
                    pLugar1 = pLugar1;
                }
                if (!RespuestaJugador2.Animal.StartsWith(Letra.ToString()) || RespuestaJugador2.Animal == null)
                {
                    pLugar2 = pLugar2;
                }

                PuntajeJugador1 = pNombre1 + pComida1 + pColor1 + pLugar1 + pAnimal1;
                PuntajeJugador2 = pNombre2 + pComida2 + pColor2 + pLugar2 + pAnimal2;
                if(PuntajeJugador1==PuntajeJugador2)
                {
                    CambiarMensaje("Empate");
                    puntaje1++;
                    puntaje2++;
                }
                bool ganaJugador1 = (PuntajeJugador1 > PuntajeJugador2);
                if(ganaJugador1)    
                {
                    CambiarMensaje($"Gana jugador {Jugador1}");
                    puntaje1++;
                }
                else
                {
                    CambiarMensaje($"Gana jugador {Jugador2}");
                    puntaje2++;
                }



                if(puntaje1<3 && puntaje2<3)
                {
                    await Task.Delay(10000);
                    CambiarMensaje("Esperando el siguiente turno");
                    await Task.Delay(10000);
                    RespuestaJugador1 = null;
                    RespuestaJugador2 = null;
                    pAnimal1 = pAnimal2 = 0;
                    pNombre1 = pNombre2 = 0;
                    pColor1 = pColor2 = 0;
                    pLugar1 = pLugar2 = 0;
                    pComida1 = pComida2 = 0;

                    PuedeJugarCliente = true;
                    PuedeJugarServidor = true;
                    Letra = ElegirLetra();
                    CambiarMensaje("Escribe tus respuestas");                    
                }
                else
                {
                    await Task.Delay(10000);
                    CambiarMensaje("Juego terminado. Ganó " + ((PuntajeJugador1 > PuntajeJugador2) ? Jugador1 : Jugador2));
                }
            }



            //Todavía no juegan los dos
        }
    }


    public class DatoEnviado
    {
        public Comando Comando { get; set; }
        public string Nombre { get; set; }
        public char LetraRandom { get; set; }

        public Respuestas DatoRespuestas { get; set; }
    }
}
