// Globalne using tylko dla jednoznacznych przestrzeni nazw BCL. Celowo NIE obejmuje System.Windows
// ani System.Windows.Forms (i pokrewnych: System.Windows.Controls, System.Windows.Media, System.Drawing) —
// UseWPF + UseWindowsForms razem definiują te same nazwy typów (Application, UserControl, Image, Point...),
// więc te przestrzenie nazw są importowane jawnie w poszczególnych plikach, gdzie są potrzebne.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
