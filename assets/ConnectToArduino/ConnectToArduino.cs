﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System;

public class ConnectToArduino : MonoBehaviour
{
    [SerializeField]
    private Color connectedColor;

    [SerializeField]
    private Color errorColor;  

    [SerializeField]
    private Color inputfieldErrorColor;

    [SerializeField]
    private InputField emailInputField;

    [SerializeField]
    private InputField serialPortInputField;

    [SerializeField]
    private InputField baudRateInputField;

    [SerializeField]
    private UnityEngine.UI.Dropdown arduinoDropdown;

    [SerializeField]
    private Text connectStatus;

    [SerializeField]
    private string redirectScene;

    public string sanitizedSerialPort = "";

    public int sanitizedBaudRate = -1;
    private float connectTimer = 0f;
    private float connectTimeout = 3f;
    private bool connectingToArduino = false;
    private string arduinoError = "";

    public string email;


    private EventSystem eventSystem;
	private static SerialPort serialport;

    void Awake()
    {
        eventSystem = EventSystem.current;
        string[] ports = SerialPort.GetPortNames();
        if (ports != null && ports.Length > 0) {
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer) {
                foreach (string port in ports) {
                    string p = port.Replace("/dev/tty","/dev/cu");
                    arduinoDropdown.AddOptions(new List<string> { p});
                }
            } else {
                arduinoDropdown.AddOptions(ports.ToList());
            }
            arduinoDropdown.AddOptions(new List<string> {"Custom.."});
            serialPortInputField.text = arduinoDropdown.options[arduinoDropdown.value].text;
        } else {
            arduinoDropdown.gameObject.SetActive(false);
            serialPortInputField.gameObject.SetActive(true);
        }
        DontDestroyOnLoad (transform.gameObject);
    }

    
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Selectable next = eventSystem.currentSelectedGameObject.GetComponent<Selectable>().FindSelectableOnDown();
        
            if (next!= null) {
                            
                InputField inputfield = next.GetComponent<InputField>();
                if (inputfield !=null) inputfield.OnPointerClick(new PointerEventData(eventSystem));
                            
                eventSystem.SetSelectedGameObject(next.gameObject, new BaseEventData(eventSystem));
            }        
        }

        // Catching exceptions from Serialport.open() freezes Unity,
        // so our error-handling work-around is a timer.
        if (connectingToArduino) {
            connectTimer += Time.deltaTime;
            if (connectTimer > connectTimeout) {
                displayArduinoError("Connection Time Out");
            }
        }
    }

    public void SkipPressed() {
        string regex = @"(^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+.[a-zA-Z0-9-.]+$)";
        var match = Regex.Match(emailInputField.text, regex, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            connectStatus.text = "Please Enter a valid E-mail address.";
            connectStatus.color = errorColor;
            emailInputField.image.color = inputfieldErrorColor;
            return;
        }
        email = emailInputField.text;
        RedirectToScene();       
    }

    public void RedirectToScene() {
            SceneManager.LoadSceneAsync(redirectScene);
    }

    private void displayArduinoError(string text = "") {
        connectStatus.text = "Could not connect to Arduino on port: " + sanitizedSerialPort;
        connectStatus.text += '\n' + "(Is the Arduino Monitor open?)";
        //connectStatus.text += '\n' + "(" + text + ")"; // use this if the disconnection does not work for unknown reasons.
        connectStatus.color = errorColor;
        serialPortInputField.image.color = inputfieldErrorColor;
        connectingToArduino = false;
        connectTimer = 0f;
    }

    private void displayBaudRateError() {
        connectStatus.text = "Invalid Baud Rate (" + sanitizedBaudRate + "). (Use fx. 115200)";
        connectStatus.color = errorColor;
        baudRateInputField.image.color = inputfieldErrorColor;
        connectingToArduino = false;
        connectTimer = 0f;
    }

    public void dropdown_Changed() {
        if (arduinoDropdown.value == arduinoDropdown.options.Count()-1) {
            // Custom Option
            arduinoDropdown.gameObject.SetActive(false);
            serialPortInputField.gameObject.SetActive(true);
            serialPortInputField.text = "";
        } else {
            serialPortInputField.text = arduinoDropdown.options[arduinoDropdown.value].text;
        }
    }

    public void ConnectPressed() {
        connectStatus.text = "Connecting...";
        string regex = @"(^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+.[a-zA-Z0-9-.]+$)";
        var match = Regex.Match(emailInputField.text, regex, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            connectStatus.text = "Please Enter a valid E-mail address.";
            connectStatus.color = errorColor;
            emailInputField.image.color = inputfieldErrorColor;
            return;
        }

        sanitizedSerialPort = new string((from c in serialPortInputField.text where char.IsLetterOrDigit(c) || char.IsPunctuation(c) select c).ToArray());
        string baudrateString = new string((from c in baudRateInputField.text where char.IsDigit(c) select c).ToArray());
        if (string.IsNullOrEmpty(baudrateString)) {
            displayBaudRateError();
            return;
        }
        try {
            sanitizedBaudRate = int.Parse(baudrateString);
        } catch (System.FormatException e) {
            displayBaudRateError();
        }

        UnityEngine.Debug.Log(sanitizedSerialPort);
        UnityEngine.Debug.Log(sanitizedBaudRate);
        serialport = new SerialPort (sanitizedSerialPort, sanitizedBaudRate);
        email = emailInputField.text;
        connectingToArduino = true;
        bool connected = OpenConnection();
        if (connected) {
            connectingToArduino = false;
            connectTimer = 0f;
            connectStatus.text = "Connected to Arduino on port: " + sanitizedSerialPort;
            connectStatus.color = connectedColor;
            CloseConnection();
            RedirectToScene();
        } else {
            displayArduinoError(arduinoError);
        }
    }

    public void serialPortInputFieldChange() {
        serialPortInputField.image.color = Color.white;
    }

    public void emailInputFieldChange() {
        emailInputField.image.color = Color.white;
    }

    public bool OpenConnection()
    {
		if (serialport != null)
		{
			if (serialport.IsOpen)
			{
				//Serial port is already open. We ignore it for now.
			}
			else
			{
                try {
                    //Open the connection to read data
                    serialport.Open();
                    //Set time-out value before reporting error
                    serialport.ReadTimeout = 100;
                    Debug.Log("Connected to Arduino, on port: " + serialport.PortName);
                    return true;
                }
                catch (Exception e) {
                    arduinoError = e.ToString();
                }
				
                
			}
		}
		return false;
    }


    // Closes the connection to the Arduino
    // Should be run before closing the Unity program
    public void CloseConnection()
    {
        if (serialport != null && serialport.IsOpen)
        {
            Debug.Log("Closing connection to Arduino, on port: " + serialport.PortName);
            //If the connection is open, we close it before ending the program
            serialport.Close();
        }
    }

}
