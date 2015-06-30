# Laird BLE Module Tool.Net

* Hardware requirements:
    * Laird BLE module
    * UART-USB Adapter
    * JLink Adapter
* Software requirements:
    * .net framework 4.0/4.5
    * JLink driver and tools
    * UART-USB dedicated driver

Communicate with BLE module by UART-USB with hard flow control.

### .config file 
* Key "Port" defines comm port, "AUTO" means automatic selection of port name.
* Key "Reflash" defines batch file name of reflashing firmware.

### Command console usage:

####  Info
      Show module infomation
####  Format
      Erase all user programs and configuration
####  Upload [file]
      .sb file means smartBasic source code, first compiled and uploaded to module
      .uwc file means compiled binary, direct uploaded to module
      Notice: valid compiler should exist.
#### Reflash
      Reflash(update) firmware of module (Need JLink Adapter with SWD)
#### Command
      Execute any AT command defined for BLE module

Regarding dedicated AT commands, please check out [Laird documents](http://www.lairdtech.com/products/bl600-series).
