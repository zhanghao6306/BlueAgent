using System;
using System.Collections.Generic;

namespace Simulators.Modbus
{
    /// <summary>
    /// PLC State Machine implementing the BlueImage platform protocol
    /// </summary>
    public class PlcStateMachine
    {
        // Status values
        private const ushort STATUS_READY = 1;
        private const ushort STATUS_OK = 1;
        private const ushort STATUS_RUNNING = 1;
        private const ushort STATUS_STOPPED = 0;
        private const ushort GARMENT_TYPE_NONE = 0;
        private const ushort GARMENT_TYPE_MIN = 1;
        private const ushort GARMENT_TYPE_MAX = 15;
        private const ushort COIL_GARMENT_TYPE_NONE = 1090;
        private const ushort COIL_GARMENT_TYPE_BASE = 1016;

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
            if (_registers.Length > PlcRegisters.ReadyStatus) _registers[PlcRegisters.ReadyStatus] = STATUS_READY;
            // Set OK state
            if (_registers.Length > PlcRegisters.OkStatus) _registers[PlcRegisters.OkStatus] = STATUS_OK;
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
            if (CheckRiseEdge(PlcRegisters.StartHandshake, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.StartHandshakeAck)
                {
                    _coils[PlcCoils.StartHandshakeAck] = true;
                    Console.WriteLine("M1000 set to 1: Acknowledge start signal D2000");
                }
            }

            // Check D2005 for client feedback acknowledgment (rise edge)
            if (CheckRiseEdge(PlcRegisters.ClientFeedbackAck, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.ClientFeedbackAck)
                {
                    _coils[PlcCoils.ClientFeedbackAck] = true;
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
            if (CheckRiseEdge(PlcRegisters.FoundationPrintComplete, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.FoundationPrintCompleteAck)
                {
                    _coils[PlcCoils.FoundationPrintCompleteAck] = true;
                    Console.WriteLine("M1001 set to 1: Foundation print complete");
                }
            }

            // White print complete (D2002 -> M1002)
            if (CheckRiseEdge(PlcRegisters.WhitePrintComplete, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.WhitePrintCompleteAck)
                {
                    _coils[PlcCoils.WhitePrintCompleteAck] = true;
                    Console.WriteLine("M1002 set to 1: White print complete");
                }
            }

            // Color print complete (D2003 -> M1003)
            if (CheckRiseEdge(PlcRegisters.ColorPrintComplete, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.ColorPrintCompleteAck)
                {
                    _coils[PlcCoils.ColorPrintCompleteAck] = true;
                    Console.WriteLine("M1003 set to 1: Color print complete");
                }
            }

            // Topcoat print complete (D2004 -> M1004)
            if (CheckRiseEdge(PlcRegisters.TopcoatPrintComplete, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.TopcoatPrintCompleteAck)
                {
                    _coils[PlcCoils.TopcoatPrintCompleteAck] = true;
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
            if (CheckRiseEdge(PlcRegisters.PlatformSoftStart, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.PlatformSoftStartAck)
                {
                    _coils[PlcCoils.PlatformSoftStartAck] = true;
                    Console.WriteLine("M1013 set to 1: Soft start request acknowledged");
                }

                // Set platform running status
                if (_registers.Length > PlcRegisters.PlatformStatus)
                {
                    _registers[PlcRegisters.PlatformStatus] = STATUS_RUNNING;
                    Console.WriteLine("D2068 set to 1: Platform is running");
                }
            }

            // Soft stop request (D2007 -> M1014)
            if (CheckRiseEdge(PlcRegisters.PlatformSoftStop, STATUS_READY))
            {
                if (_coils.Length > PlcCoils.PlatformSoftStopAck)
                {
                    _coils[PlcCoils.PlatformSoftStopAck] = true;
                    Console.WriteLine("M1014 set to 1: Soft stop request acknowledged");
                }

                // Set platform stopped status
                if (_registers.Length > PlcRegisters.PlatformStatus)
                {
                    _registers[PlcRegisters.PlatformStatus] = STATUS_STOPPED;
                    Console.WriteLine("D2068 set to 0: Platform is stopped");
                }
            }

            // Check if client confirmed platform running (M1068)
            if (_coils.Length > PlcCoils.PlatformStatusAck && _coils[PlcCoils.PlatformStatusAck])
            {
                Console.WriteLine("M1068: Client confirmed platform running");
            }
        }

        /// <summary>
        /// Handle garment type signals (D2050)
        /// </summary>
        private void HandleGarmentType()
        {
            if (_registers.Length <= PlcRegisters.GarmentType) return;

            ushort garmentType = _registers[PlcRegisters.GarmentType];

            // Only process if value has changed
            if (!_previousRegisterValues.TryGetValue(PlcRegisters.GarmentType, out ushort oldValue) || oldValue != garmentType)
            {
                // Reset all garment type coils
                for (int i = COIL_GARMENT_TYPE_BASE; i <= COIL_GARMENT_TYPE_BASE + (GARMENT_TYPE_MAX - GARMENT_TYPE_MIN); i++)
                {
                    if (i < _coils.Length)
                    {
                        _coils[i] = false;
                    }
                }

                if (garmentType == GARMENT_TYPE_NONE)
                {
                    if (_coils.Length > COIL_GARMENT_TYPE_NONE)
                    {
                        _coils[COIL_GARMENT_TYPE_NONE] = true;
                        Console.WriteLine("M1090 set to 1: Garment type 0 (none) confirmed");
                    }
                }
                else if (garmentType >= GARMENT_TYPE_MIN && garmentType <= GARMENT_TYPE_MAX)
                {
                    int coilAddr = COIL_GARMENT_TYPE_BASE + (garmentType - GARMENT_TYPE_MIN);
                    if (coilAddr < _coils.Length)
                    {
                        _coils[coilAddr] = true;
                        Console.WriteLine($"M{coilAddr} set to 1: Garment type {garmentType} confirmed");
                    }
                }
                else
                {
                    Console.WriteLine($"Invalid garment type: {garmentType}. Valid range: {GARMENT_TYPE_NONE}-{GARMENT_TYPE_MAX}");
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
            ushort[] importantRegisters = { 
                PlcRegisters.StartHandshake, 
                PlcRegisters.FoundationPrintComplete, 
                PlcRegisters.WhitePrintComplete, 
                PlcRegisters.ColorPrintComplete, 
                PlcRegisters.TopcoatPrintComplete, 
                PlcRegisters.ClientFeedbackAck, 
                PlcRegisters.PlatformSoftStart, 
                PlcRegisters.PlatformSoftStop, 
                PlcRegisters.GarmentType 
            };

            foreach (ushort reg in importantRegisters)
            {
                if (reg < _registers.Length)
                {
                    _previousRegisterValues[reg] = _registers[reg];
                }
            }
        }
    }
}