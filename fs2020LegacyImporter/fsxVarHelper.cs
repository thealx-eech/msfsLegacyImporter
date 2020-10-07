using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace msfsLegacyImporter
{
    class fsxVarHelper
    {
        public string fsx2msfsSimVar(string fsxSimVar)
        {
            fsxSimVar = Regex.Replace(fsxSimVar, "\r\n|\r|\n", "");
            fsxSimVar = Regex.Replace(fsxSimVar, @"\s\s+", " ");

            string fsxVariable = fsxSimVar;
            List<string[]> variables = new List<string[]>();

            // REPLACE VARIABLES
            Random r = new Random();
            //var regex = new Regex(@"(.*).*{.*}.*{.*}|\((.*?)\)");
            var regex = new Regex(@"\((.*?)\)");
            foreach (var match in regex.Matches(fsxVariable))
            {
                string placeholder = r.Next(1000000, 9000000).ToString();
                variables.Add(new string[] { match.ToString(), placeholder });
                fsxVariable = fsxVariable.Replace(match.ToString(), placeholder);
            }

            // PARSE FSX FORMULA
            string infix = PostfixToInfix(fsxVariable);

            // INSERT VARIABLES
            if (!String.IsNullOrEmpty(infix))
            {
                foreach (string[] variable in variables)
                {
                    string msfsVariable = getMsfsVariable(variable[0]);
                    infix = infix.Replace(variable[1], msfsVariable);
                }

                // REMOVE TRALING SYMBOLS
                infix = infix.Trim();
                if (infix[infix.Length - 1] == '+' || infix[infix.Length - 1] == '*' || infix[infix.Length - 1] == '/' || infix[infix.Length - 1] == '-')
                    infix = infix.Substring(infix.Length - 1).Trim();

                Console.WriteLine("Orig: " + fsxSimVar + " / Final: " + infix);

                if (!String.IsNullOrEmpty(infix) && infix.Length > 1)
                    return "var ExpressionResult = " + infix + "; /* PARSED FROM \"" + fsxSimVar + "\" */";
            }

            Console.WriteLine("NOT PARSED " + fsxSimVar);
            return "var ExpressionResult = 0; /* SIM VAR \"" + fsxSimVar + "\" NOT PARSED! */";
        }

        private string getMsfsVariable(string fsxVariable)
        {
            string fsxVar = fsxVariable.Trim().Replace("( ", "(").Replace(" )", ")").Replace(" ,", ",").Replace(", ", ",").Replace("{ ", "{").
                Replace(" {", "{").Replace("} ", "}").Replace(" }", "}").Replace(": ", ":").Replace(" :", ":").Replace(", ", ",").Replace(" ,", ",");
            switch (fsxVar)
            {
                // 0 = ok, 1 = fail, 2 = blank.
                case string ha when ha.Equals("(A:PARTIAL PANEL ADF,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hb when hb.Equals("(A:PARTIAL PANEL AIRSPEED,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hc when hc.Equals("(A:PARTIAL PANEL ALTIMETER,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hd when hd.Equals("(A:PARTIAL PANEL ATTITUDE,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string he when he.Equals("(A:PARTIAL PANEL COMM,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hf when hf.Equals("(A:PARTIAL PANEL COMPASS,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hg when hg.Equals("(A:PARTIAL PANEL ELECTRICAL,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hh when hh.Equals("(A:PARTIAL PANEL AVIONICS,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hi when hi.Equals("(A:PARTIAL PANEL ENGINE,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hj when hj.Equals("(A:PARTIAL PANEL FUEL INDICATOR,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:PARTIAL PANEL HEADING,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:PARTIAL PANEL VERTICAL VELOCITY,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hm when hm.Equals("(A:PARTIAL PANEL TRANSPONDER,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hn when hn.Equals("(A:PARTIAL PANEL NAV,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PARTIAL PANEL PITOT,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:PARTIAL PANEL TURN COORDINATOR,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hq when hq.Equals("(A:PARTIAL PANEL VACUUM,enum)", StringComparison.InvariantCultureIgnoreCase):
                    return "0";

                case string hn when hn.Equals("(A:Airspeed select indicated or true,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED TRUE\", \"knots\"))";
                case string hn when hn.Equals("(A:Vertical speed,feet per minute)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"feet per minute\"))";
                case string hn when hn.Equals("(A:Vertical speed,meters per minute)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"meters per minute\"))";
                case string hn when hn.Equals("(A:Indicated Altitude,meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE\", \"meters\"))";
                case string hn when hn.Equals("(A:Indicated Altitude,feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE\", \"feet\"))";
                case string hn when hn.Equals("(A:TURN COORDINATOR BALL,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "Math.min(30, Math.max(-30 , -parseFloat(SimVar.GetSimVarValue(\"ATTITUDE INDICATOR BANK DEGREES\", \"degree\")))) / 30 * 100";
                case string hn when hn.Equals("(A:Delta Heading Rate,rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(-SimVar.GetSimVarValue(\"TURN INDICATOR RATE\", \"degree per second\")) * 60 / 360";
                case string hn when hn.Equals("(A:Variometer rate,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRCRAFT WIND Y\", \"knots\"))"; // TODO: fix rate
                case string hn when hn.Equals("(A:Wiskey compass indication degrees,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"degrees\"))";
                case string hn when hn.Equals("(A:Magnetic compass,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"radians\"))";
                case string hn when hn.Equals("(A:Fuel tank total level,position)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:Fuel tank center level,position)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL QUANTITY\", \"gallons\")) / parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL CAPACITY\", \"gallons\"))";
                case string hn when hn.Equals("(A:AMBIENT TEMPERATURE,Celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT TEMPERATURE\", \"celsius\"))";
                case string hn when hn.Equals("(A:Kohlsman setting hg,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG\", \"inches of mercury\"))";
                case string hn when hn.Equals("(A:ELECTRICAL MASTER BATTERY,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(P:Units of measure,enum)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";
                case string hn when hn.Equals("(A:AIRSPEED INDICATED,mph)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED INDICATED\", \"mph\"))";//???
                case string hn when hn.Equals("(A:SUCTION PRESSURE,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"SUCTION PRESSURE\", \"inch of mercury\"))";
                case string hn when hn.Equals("(A:GENERAL ENG1 OIL PRESSURE,PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:1\", \"psi\"))";
                case string hn when hn.Equals("(A:GENERAL ENG2 OIL PRESSURE,PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:2\", \"psi\"))";
                case string hk when hk.Equals("(A:GENERAL ENG1 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE:1\", \"celsius\"))";
                case string hk when hk.Equals("(A:GENERAL ENG2 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE:2\", \"celsius\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:1,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:1\", \"psi\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:2,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:2\", \"psi\"))";
                case string hn when hn.Equals("(A:RADIO HEIGHT,meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RADIO HEIGHT\", \"meters\"))";
                case string hn when hn.Equals("(A:RADIO HEIGHT,feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RADIO HEIGHT\", \"feet\"))";
                case string hn when hn.Equals("(A:DECISION HEIGHT,feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"DECISION HEIGHT\", \"feet\"))";
                case string hn when hn.Equals("(A:DECISION HEIGHT,meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"DECISION HEIGHT\", \"meters\"))";
                case string hn when hn.Equals("(P:Local time,seconds)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"seconds\"))";
                case string hn when hn.Equals("(P:Local time,minutes)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"minutes\"))";
                case string hn when hn.Equals("(P:Local time,hours)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"hours\"))";
                case string hn when hn.Equals("(A:GENERAL ENG1 RPM,rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG RPM:2\", \"Rpm\"))";
                case string hn when hn.Equals("(A:GENERAL ENG2 RPM,rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG RPM:1\", \"Rpm\"))";
                case string hn when hn.Equals("(A:Plane heading degrees gyro,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:heading indicator:1,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:heading indicator:2,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"degrees\"))";//???
                case string hn when hn.Equals("(A:PLANE HEADING DEGREES GYRO,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:heading indicator:1,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:heading indicator:2,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"radians\"))";
                case string hn when hn.Equals("(A:Autopilot heading lock dir,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT HEADING LOCK DIR\", \"degrees\"))";//???
                case string hn when hn.Equals("(A:RUDDER TRIM PCT,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RUDDER TRIM PCT\", \"percent\"))";
                case string hn when hn.Equals("(A:ELEVATOR TRIM POSITION,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELEVATOR TRIM PCT\", \"percent\"))";
                case string hn when hn.Equals("(A:NAV GSI:1,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV GSI:1\", \"percent\"))";
                case string hn when hn.Equals("(A:NAV GSI:2,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV GSI:2\", \"percent\"))";
                case string hn when hn.Equals("(A:TRAILING EDGE FLAPS LEFT ANGLE,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS RIGHT ANGLE\", \"radians\"))";
                case string hn when hn.Equals("(A:ENG1 MANIFOLD PRESSURE,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG MANIFOLD PRESSURE:1\", \"inHG\"))";
                case string hn when hn.Equals("(A:ENG2 MANIFOLD PRESSURE,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG MANIFOLD PRESSURE:2\", \"inHG\"))";
                case string hn when hn.Equals("(A:Attitude indicator bank degrees,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ATTITUDE INDICATOR BANK DEGREES\", \"radians\"))";
                case string hn when hn.Equals("(L:Show Volts 1,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";//"parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL MAIN BUS VOLTAGE:1\", \"volts\"))";
                case string hn when hn.Equals("(L:Show Volts 2,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";//parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL MAIN BUS VOLTAGE:2\", \"volts\"))";
                case string hn when hn.Equals("(A:RECIP CARBURETOR TEMPERATURE:1,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG1 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG OIL TEMPERATURE:1\", \"celsius\")) * 0.5";//??
                case string hn when hn.Equals("(A:RECIP CARBURETOR TEMPERATURE:2,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG2 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG OIL TEMPERATURE:2\", \"celsius\")) * 0.5";//??
                case string hn when hn.Equals("(A:RECIP ENG CYLINDER HEAD TEMPERATURE:1,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG CYLINDER HEAD TEMPERATURE:1\", \"celsius\"))";
                case string hn when hn.Equals("(A:RECIP ENG CYLINDER HEAD TEMPERATURE:2,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG CYLINDER HEAD TEMPERATURE:2\", \"celsius\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS VOLTAGE:1,volts)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS VOLTAGE:1\", \"volts\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS VOLTAGE:2,volts)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS VOLTAGE:2\", \"volts\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS AMPS:1,amps)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS AMPS:1\", \"amperes\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS AMPS:2,amps)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS AMPS:2\", \"amperes\"))";
                case string hn when hn.Equals("(A:PLANE HEADING DEGREES GYRO,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"radians\"))";
                case string hn when hn.Equals("(A:NAV1 OBS,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV1 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:1\", \"degrees\"))";
                case string hn when hn.Equals("(A:NAV2 OBS,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV2 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:2\", \"degrees\"))";
                case string hn when hn.Equals("(A:NAV1 OBS,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV1 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:1\", \"radians\"))";
                case string hn when hn.Equals("(A:NAV2 OBS,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV2 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:2\", \"radians\"))";
                case string hn when hn.Equals("(A:ADF radial:1,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF1 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:1\", \"radians\"))";
                case string hn when hn.Equals("(A:ADF radial:2,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF2 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:2\", \"radians\"))";
                case string hn when hn.Equals("(A:ADF radial:1,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF1 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:1\", \"degrees\"))";
                case string hn when hn.Equals("(A:ADF radial:2,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF2 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:2\", \"degrees\"))";
                case string hn when hn.Equals("(A:Eng1 oil quantity,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL QUANTITY:1\", \"percent\"))";
                case string hn when hn.Equals("(A:Eng2 oil quantity,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL QUANTITY:2\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng1 N1,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N1 RPM:1\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng2 N1,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N1 RPM:2\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng1 N2,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM:1\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng2 N2,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM:2\", \"percent\"))";
                case string hn when hn.Equals("(A:General eng1 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG EXHAUST GAS TEMPERATURE:1\", \"celsius\"))";
                case string hn when hn.Equals("(A:General eng2 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG EXHAUST GAS TEMPERATURE:2\", \"celsius\"))";
                case string hn when hn.Equals("", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat()";
                case string hn when hn.Equals("", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat()";
                case string hn when hn.Equals("", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat()";
                case string hn when hn.Equals("", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat()";
                // TRY TO COPY 1IN1
                default:
                    Console.WriteLine("FSX variable not found: " + fsxVar);
                    fsxVar = Regex.Replace(fsxVar, @"(\([A-Za-z]:)|\(|\)", "");

                    if (fsxVar.Contains(",") && fsxVar.Split(',').Length == 2)
                    {
                        string converted = "parseFloat(SimVar.GetSimVarValue(\"" + fsxVar.Split(',')[0].Trim() + "\", \"" + fsxVar.Split(',')[1].Trim() + "\"))";
                        Console.WriteLine("Conversion result: " + converted);
                        return converted;
                    }

                    return "0";
            }
        }

        // POSTFIX CONVERTER BY W. Michael Perkins STARTS
        public class Intermediate
        {
            public string expr;     // subexpression string
            public string oper;     // the operator used to create this expression

            public Intermediate(string expr, string oper)
            {
                this.expr = expr;
                this.oper = oper;
            }
        }

        public string PostfixToInfix(string postfix)
        {
            try
            {
                int ifElseState = 0;

                postfix = postfix.Replace("if {", "if{").Replace("} els", "}els").Replace("els {", "els{").Trim();

                // CHECK TRAILING BRACKET
                //if (postfix.Length > 0 && postfix[postfix.Length - 1] == '}' && postfix.Count(f => f == '{') < postfix.Count(f => f == '}'))
                if (postfix.Length > 0 && postfix[postfix.Length - 1] == '}' && ((postfix.Split('{').Length - 1) < (postfix.Split('}').Length - 1)))
                    postfix = postfix.TrimEnd('}').Trim();

                var postfixTokens = postfix.Split(' ');

                string lastToken = "";

                var stack = new Stack<Intermediate>();

                foreach (string token in postfixTokens)
                {
                    lastToken = token;

                    string newExpr;

                    Console.WriteLine("Token: " + token);

                    if (token == "==" || token == "!=" || token == ">" || token == "<" || token == ">=" || token == "<=")
                    {
                        var rightIntermediate = stack.Pop();
                        var leftIntermediate = stack.Pop();
                        newExpr = "( " + leftIntermediate.expr + " " + token + " " + rightIntermediate.expr + " )";
                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token.ToLower() == "if{")
                    {
                        ifElseState = 1;

                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " ( " + rightIntermediate.expr + " ? ";
                        }
                        else if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "( " + rightIntermediate.expr + " ? ";
                        }
                        else
                        {
                            newExpr = "";
                        }

                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token.ToLower() == "}els{")
                    {
                        var rightIntermediate = stack.Pop();
                        var leftIntermediate = stack.Pop();
                        newExpr = leftIntermediate.expr + " " + rightIntermediate.expr + " : ";
                        stack.Push(new Intermediate(newExpr, token));

                        ifElseState = 0;
                    }
                    else if (token == "}")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " " + rightIntermediate.expr + (ifElseState == 1 ? " : \"\"" : "") + " )";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else if (stack.Count > 0)
                        {
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + (ifElseState == 1 ? " : \"\"" : "") + " )";
                            stack.Push(new Intermediate(newExpr, token));
                        }

                        ifElseState = 0;
                    }
                    else if (token == "&&" || token.ToLower() == "AND")
                    {
                        var rightIntermediate = stack.Pop();
                        var leftIntermediate = stack.Pop();
                        newExpr = leftIntermediate.expr + " && " + rightIntermediate.expr;
                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token == "||" || token.ToLower() == "or")
                    {
                        var rightIntermediate = stack.Pop();
                        var leftIntermediate = stack.Pop();
                        newExpr = leftIntermediate.expr + " || " + rightIntermediate.expr;
                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token == "!" || token.ToLower() == "not")
                    {
                        var leftIntermediate = stack.Pop();
                        stack.Push(new Intermediate("!" + leftIntermediate.expr, token));
                    }
                    else if (token == "/-/")
                    {
                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = rightIntermediate.expr + " * (-1)";
                        }
                        else
                        {
                            newExpr = "(-1)";
                        }

                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token == "+" || token == "-")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " " + token + " " + rightIntermediate.expr;
                        }
                        else if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = rightIntermediate.expr + " " + token;
                        }
                        else
                        {
                            newExpr = token;
                        }

                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token == "*" || token == "/" || token == "%")
                    {
                        string leftExpr, rightExpr;

                        var rightIntermediate = stack.Pop();
                        if (rightIntermediate.oper == "+" || rightIntermediate.oper == "-")
                        {
                            rightExpr = "(" + rightIntermediate.expr + ")";
                        }
                        else
                        {
                            rightExpr = rightIntermediate.expr;
                        }

                        if (stack.Count > 0)
                        {
                            var leftIntermediate = stack.Pop();
                            if (leftIntermediate.oper == "+" || leftIntermediate.oper == "-")
                            {
                                leftExpr = "(" + leftIntermediate.expr + ")";
                            }
                            else
                            {
                                leftExpr = leftIntermediate.expr;
                            }
                        }
                        else
                        {
                            leftExpr = "";
                        }

                        newExpr = leftExpr + " " + token + " " + rightExpr;

                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else
                    {
                        if (Regex.IsMatch(token, @"^[-0-9.]+$"))
                        {
                            // NUMBER
                            stack.Push(new Intermediate(token, ""));
                        }
                        else
                        {
                            // OPERATOR
                            switch (token)
                            {
                                case "dnor":
                                case "d360":
                                case "rdeg":
                                case "rnor":
                                    // JUST SKIP
                                    break;

                                case "pi":
                                    stack.Push(new Intermediate("Math.PI", ""));
                                    break;
                                case "dgrd":
                                    addExpression(stack, token, "* Math.PI/180", "after");
                                    break;
                                case "rddg":
                                    addExpression(stack, token, "* 180/Math.PI", "after");
                                    break;
                                case "abs":
                                    addExpression(stack, token, "Math.abs", "before");
                                    break;
                                case "int":
                                case "flr":
                                    addExpression(stack, token, "Math.floor", "before");
                                    break;
                                case "cos":
                                    addExpression(stack, token, "Math.cos", "before");
                                    break;
                                case "sin":
                                    addExpression(stack, token, "Math.sin", "before");
                                    break;
                                case "acos":
                                    addExpression(stack, token, "Math.acos", "before");
                                    break;
                                //case "ctg":
                                //    addExpression(stack, token, "Math.cot", "before");
                                //    break;
                                case "ln":
                                    addExpression(stack, token, "Math.log", "before");
                                    break;
                                case "sqr":
                                    addExpression(stack, token, " ** 2", "after");
                                    break;
                                case "asin":
                                    addExpression(stack, token, "Math.asin", "before");
                                    break;
                                case "sqrt":
                                    addExpression(stack, token, "Math.sqrt", "before");
                                    break;
                                case "exp":
                                    addExpression(stack, token, "Math.exp", "before");
                                    break;
                                case "tg":
                                    addExpression(stack, token, "Math.tan", "before");
                                    break;
                                case "atg":
                                    addExpression(stack, token, "Math.atan", "before");
                                    break;
                                case "ceil":
                                    addExpression(stack, token, "Math.ceil", "before");
                                    break;
                                case "near":
                                    addExpression(stack, token, "Math.round", "before");
                                    break;
                                case "min":
                                    addExpression(stack, token, "Math.min", "before");
                                    break;
                                case "max":
                                    addExpression(stack, token, "Math.max", "before");
                                    break;
                                //case "log":
                                    //addExpression(stack, token, "Math.log(val) / Math.log(10)", "before");
                                    //break;
                                case "pow":
                                    addExpression(stack, token, "Math.pow", "before");
                                    break;
                                //case "div":
                                    //addExpression(stack, token, "Math.floor(y / x)", "before");
                                    //break;


                                case "b":
                                case "c":
                                case "d":
                                case "p":
                                case "r":
                                default:
                                    Console.WriteLine("Unknown operator \""+ token + "\"");
                                    return "";

                            }

                        }
                    }
                }

                foreach (var obj in stack)
                {
                    Console.WriteLine("\"" + obj.expr + "\" [" + obj.oper + "]");
                }
                if (stack.Count > 0)
                    return stack.Peek().expr;
                else
                    return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return "";
            }
        }

        private void addExpression(Stack<Intermediate> stack, string token, string modifier, string position, string lastToken = "")
        {
            string newExpr = "";

            if (stack.Count > 1)
            {
                var rightIntermediate = stack.Pop();
                var leftIntermediate = stack.Pop();
                newExpr = (position == "before" ? modifier : "") + " ( " + leftIntermediate.expr + ", " + rightIntermediate.expr + " ) " + (position == "after" ? modifier : "");
            }
            else if (stack.Count > 0)
            {
                var rightIntermediate = stack.Pop();
                newExpr = (position == "before" ? modifier : "") + " ( " + rightIntermediate.expr + " ) " + (position == "after" ? modifier : "");
            }
            else
            {
                newExpr = (position == "before" ? modifier + "()" : "") + (position == "after" ? "1" + modifier : "");
            }

            stack.Push(new Intermediate(newExpr, token));
        }
        // POSTFIX CONVERTER ENDS
    }
}
