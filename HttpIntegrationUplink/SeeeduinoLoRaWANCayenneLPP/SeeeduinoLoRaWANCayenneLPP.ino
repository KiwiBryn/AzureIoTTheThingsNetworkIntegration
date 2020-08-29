#include <LoRaWan.h>
#include<CayenneLPP.h> 

CayenneLPP lpp(64);
char buffer[256];

void setup(void)
{
    SerialUSB.begin(9600);
    while(!SerialUSB);

    lora.init();

    memset(buffer, 0, 256);
    lora.getVersion(buffer, 256, 1);
    SerialUSB.print("Ver:");
    SerialUSB.print(buffer); 
 
    memset(buffer, 0, 256);
    lora.getId(buffer, 256, 1);
    SerialUSB.print("ID:");
    SerialUSB.println(buffer);

    lora.setKey(NULL, NULL, "12345678901234567890123456789012");
    lora.setId(NULL, "1234567890123456", "1234567890123456");

    lora.setPort(10);
        
    lora.setDeciveMode(LWOTAA);
    lora.setDataRate(DR0, AS923);

    lora.setDutyCycle(false);
    lora.setJoinDutyCycle(false);
 
    lora.setPower(14);

    while(!lora.setOTAAJoin(JOIN, 10));
}
 
void loop(void)
{   
    bool result = false;

    lpp.reset ();

    // Original LPPv1 data types only these work
    // https://www.thethingsnetwork.org/docs/devices/arduino/api/cayennelpp.html
    // https://loranow.com/cayennelpp/
    //
    lpp.addTemperature (1, 27.2);
    //lpp.addTemperature (2, 25.5);
    lpp.addLuminosity(1, 100);    
    lpp.addDigitalInput(1, true);    
    //lpp.addDigitalInput(2, false);    
    lpp.addAnalogInput(1, 0.5);
    lpp.addGPS (1, 4.34, 40.22, 755);

    // Core data types all worked
    //lpp.addDigitalInput(1, true);    
    //uint8_t addDigitalOutput(uint8_t channel, uint8_t value);
    //lpp.addAnalogInput(1, 0.5);
    //uint8_t addAnalogOutput(uint8_t channel, float value);
    //lpp.addLuminosity(1, 100);    
    //uint8_t addPresence(uint8_t channel, uint8_t value);
    //lpp.addTemperature (1, 27.2);
    //uint8_t addRelativeHumidity(uint8_t channel, float rh);
    //uint8_t addAccelerometer(uint8_t channel, float x, float y, float z);
    //uint8_t addBarometricPressure(uint8_t channel, float hpa);
    //uint8_t addGyrometer(uint8_t channel, float x, float y, float z);
    //lpp.addGPS (1, 4.34, 40.22, 755)

    // Additional data types don't think any of these worked
    ///lpp.addUnixTime(1, millis()); 
    //lpp.addGenericSensor(1, 1.23456);
    //lpp.addVoltage(1, 4.5);
    //lpp.addCurrent(0, 1.0);
    //lpp.addFrequency (1, 50); 
    //lpp.addPercentage(1, 50);
    //lpp.addAltitude(1, 20.5);
    //lpp.addPower(1, 1500);
    //lpp.addDistance(1, 120.0);
    //lpp.addEnergy(1, 2.345);
    //lpp.addDirection(1, -98.76);
    //lpp.addSwitch(0, 1);
    //lpp.addConcentration(0, 10);
    //lpp.addColour(1, 255, 255, 255);

    uint8_t *lppBuffer = lpp.getBuffer();
    uint8_t lppLen = lpp.getSize();

    SerialUSB.print("Length is: ");
    SerialUSB.println(lppLen);

    // Dump buffer content for debugging
    PrintHexBuffer (lppBuffer, lppLen);    

    //result = lora.transferPacket("Hello World!", 10);
    result = lora.transferPacket(lppBuffer, lppLen);

    if(result)
    {
        short length;
        short rssi;
        //char buffer[64];          
 
        memset(buffer, 0, sizeof(buffer));
        length = lora.receivePacket(buffer, 256, &rssi);
 
        if(length)
        {
            SerialUSB.print("Length is: ");
            SerialUSB.println(length);
            SerialUSB.print("RSSI is: ");
            SerialUSB.println(rssi);
            SerialUSB.print("Data is: ");
            for(unsigned char i = 0; i < length; i ++)
            {
                SerialUSB.print("0x");
                SerialUSB.print(buffer[i], HEX);
                SerialUSB.print(" ");
            }
            SerialUSB.println();
        }
    }
    delay( 30000);
}

void PrintHexBuffer( uint8_t *buffer, uint8_t size )
{

    for( uint8_t i = 0; i < size; i++ )
    {
        if(buffer[i] < 0x10)
        {
            Serial.print('0');
        }
        SerialUSB.print( buffer[i], HEX );
        Serial.print(" ");
    }
    SerialUSB.println( );
}
