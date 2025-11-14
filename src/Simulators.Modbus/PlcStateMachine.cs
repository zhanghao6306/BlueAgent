using System;
using System.Collections.Generic;

/// <summary>
/// PLC State Machine implementing the BlueImage platform protocol
/// </summary>
public class PlcStateMachine
    {
        private ushort[] _registers;
        private bool[] _coils;
        private Dictionary<ushort, ushort> _previousRegisterValues;

        /// <summary>
        /// Initialize the PLC state machine
        /// </summary>
        /// <param name="registers">Holding registers</param>
        /// <param name="coils">Coils</param>
        public PlcStateMachine(ushort[] registers, bool[] coils)
        {
            _registers = registers;
            _coils = coils;
            _previousRegisterValues = new Dictionary<ushort, ushort>();

            // Initialize platform running status registers
            InitializePlatformRegisters();
        }

        /// <summary>
        /// Initialize platform running status registers (D2060-D2071)
        /// </summary>
        private void InitializePlatformRegisters()
        {
            // Set Ready state
            if (_registers.Length > 2060) _registers[2060] = 1;
            // Set OK state
            if (_registers.Length > 2069) _registers[2069] = 1;
        }

        /// <summary>
        /// Update the PLC state machine
        /// </summary>
        public void Update()
        {
            // Check start handshake signals
            HandleStartHandshake();

            // Check print complete signals
            HandlePrintCompleteSignals();

            // Check platform start/stop signals
            HandlePlatformStartStop();

            // Check garment type signals
            HandleGarmentType();

            // Update previous register values for rise edge detection
            UpdatePreviousRegisterValues();
        }

        /// <summary>
        /// Handle start handshake signals (D2000, M1000, D2005, M1015)
        /// </summary>
        private void HandleStartHandshake()
        {
            // Check D2000 for start signal (rise edge)
            if (CheckRiseEdge(2000, 1))
            {
                if (_coils.Length > 1000)
                {
                    _coils[1000] = true;
                    Console.WriteLine("M1000 set to 1: Acknowledge start signal D2000");
                }
            }

            // Check D2005 for client feedback acknowledgment (rise edge)
            if (CheckRiseEdge(2005, 1))
            {
                if (_coils.Length > 1015)
                {
                    _coils[1015] = true;
                    Console.WriteLine("M1015 set to 1: Acknowledge feedback D2005");
                }
            }
        }

        /// <summary>
        /// Handle print complete signals from various modules
        /// </summary>
        private void HandlePrintCompleteSignals()
        {
            // Foundation print complete (D2001 -> M1001)
            if (CheckRiseEdge(2001, 1))
            {
                if (_coils.Length > 1001)
                {
                    _coils[1001] = true;
                    Console.WriteLine("M1001 set to 1: Foundation print complete");
                }
            }

            // White print complete (D2002 -> M1002)
            if (CheckRiseEdge(2002, 1))
            {
                if (_coils.Length > 1002)
                {
                    _coils[1002] = true;
                    Console.WriteLine("M1002 set to 1: White print complete");
                }
            }

            // Color print complete (D2003 -> M1003)
            if (CheckRiseEdge(2003, 1))
            {
                if (_coils.Length > 1003)
                {
                    _coils[1003] = true;
                    Console.WriteLine("M1003 set to 1: Color print complete");
                }
            }

            // Topcoat print complete (D2004 -> M1004)
            if (CheckRiseEdge(2004, 1))
            {
                if (_coils.Length > 1004)
                {
                    _coils[1004] = true;
                    Console.WriteLine("M1004 set to 1: Topcoat print complete");
                }
            }
        }

        /// <summary>
        /// Handle platform start/stop signals
        /// </summary>
        private void HandlePlatformStartStop()
        {
            // Soft start request (D2006 -> M1013)
            if (CheckRiseEdge(2006, 1))
            {
                if (_coils.Length > 1013)
                {
                    _coils[1013] = true;
                    Console.WriteLine("M1013 set to 1: Soft start request acknowledged");
                }

                // Set platform running status
                if (_registers.Length > 2068)
                {
                    _registers[2068] = 1;
                    Console.WriteLine("D2068 set to 1: Platform is running");
                }
            }

            // Soft stop request (D2007 -> M1014)
            if (CheckRiseEdge(2007, 1))
            {
                if (_coils.Length > 1014)
                {
                    _coils[1014] = true;
                    Console.WriteLine("M1014 set to 1: Soft stop request acknowledged");
                }

                // Set platform stopped status
                if (_registers.Length > 2068)
                {
                    _registers[2068] = 0;
                    Console.WriteLine("D2068 set to 0: Platform is stopped");
                }
            }

            // Check if client confirmed platform running (M1068)
            if (_coils.Length > 1068 && _coils[1068])
            {
                Console.WriteLine("M1068: Client confirmed platform running");
            }
        }

        /// <summary>
        /// Handle garment type signals (D2050)
        /// </summary>
        private void HandleGarmentType()
        {
            if (_registers.Length <= 2050) return;

            ushort garmentType = _registers[2050];

            // Only process if value has changed
            if (!_previousRegisterValues.TryGetValue(2050, out ushort oldValue) || oldValue != garmentType)
            {
                // Reset all garment type coils
                for (int i = 1016; i <= 1030; i++)
                {
                    if (i < _coils.Length)
                    {
                        _coils[i] = false;
                    }
                }

                if (garmentType == 0)
                {
                    if (_coils.Length > 1090)
                    {
                        _coils[1090] = true;
                        Console.WriteLine("M1090 set to 1: Garment type 0 (none) confirmed");
                    }
                }
                else if (garmentType >= 1 && garmentType <= 15)
                {
                    int coilAddr = 1016 + (garmentType - 1);
                    if (coilAddr < _coils.Length)
                    {
                        _coils[coilAddr] = true;
                        Console.WriteLine($"M{coilAddr} set to 1: Garment type {garmentType} confirmed");
                    }
                }
                else
                {
                    Console.WriteLine($"Invalid garment type: {garmentType}. Valid range: 0-15");
                }
            }
        }

        /// <summary>
        /// Check for a rise edge (0 -> 1) on a register
        /// </summary>
        /// <param name="address">Register address</param>
        /// <param name="expectedValue">Expected value after rise</param>
        /// <returns>True if rise edge detected, false otherwise</returns>
        private bool CheckRiseEdge(ushort address, ushort expectedValue)
        {
            if (address >= _registers.Length) return false;

            ushort currentValue = _registers[address];
            bool riseEdge = false;

            if (_previousRegisterValues.TryGetValue(address, out ushort oldValue))
            {
                riseEdge = oldValue != expectedValue && currentValue == expectedValue;
            }
            else
            {
                riseEdge = currentValue == expectedValue;
            }

            return riseEdge;
        }

        /// <summary>
        /// Update previous register values for rise edge detection
        /// </summary>
        private void UpdatePreviousRegisterValues()
        {
            // Update values for important registers
            ushort[] importantRegisters = { 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2050 };

            foreach (ushort reg in importantRegisters)
            {
                if (reg < _registers.Length)
                {
                    _previousRegisterValues[reg] = _registers[reg];
                }
            }
        }
}