﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarControl : MonoBehaviour {

	public bool manual = true;
	//the wheel colliders for the car
	public Transform[] wheels;
	//the rigidbody of the car
	private Rigidbody rb; 

	//whether or not this car is currently avoiding objects
	/*private bool avoidObject; 
	private float angleObject;
	private Vector3 posObject; */
	private float LeftDis;
	private float CenterDis;
	private float RightDis;
	private bool leftObj;
	private bool centerObj;
	private bool rightObj;

	//These are gear ratios that are the ratios to apply the torque to the wheels for maximum realism
	public float[] GearRatio;
	public int CurrGear = 0;

	//the input power,steer, and RPM to the vehicle
	private float input_torque;
	private float input_steer;
	private float input_rpm;
	private float brake_power; 

	//the maximum steering sharpness for the car
	public float maxSteer = 12.0f;
	//the engine power 
	public float engineTorque = 150.0f;
	//the brake torque
	public float brakeTorque = 30.0f;

	//the maximum and minimum rpm for the engine
	public float maxEngineRPM = 3000.0f;
	public float minEngineRPM = 1000.0f;
	public float maxSpeed = 30f; 
	
	// Objects holding the way points for car movement
	public GameObject wayPointObject;
	private int current_point = 0;

	// Variables for the dynamic path creation
	private Node startNode = null; //original start node
	private List<Vector3> currentWayPoints = null; //waypoints of the current path
	private List<Vector3> nextWayPoints = null; //waypoints of the next path which are calculated
										 //in a thread
	private bool jobInProgress = false; //indicates if a thread job is currently in progress
	private bool pathCalculated = false; //indicates if next path segment has been calculated
	private DynamicPathThreadJob currentThreadJob = null; //the current dyanmic path thread job

	// Represents the kart object that simulation will need to interface with path planning code
	Kart kart;

	//Initialize center of mass to prevent car from flipping over too much,
	//Path plan by calculating a graph from the map and determining the waypoints
	void Start () {
		rb = GetComponent<Rigidbody>();
		rb.centerOfMass = new Vector3(0.0f,-0.3f,0.7f);
		InitialPathPlan();
		Debug.Log ("Initial Path Plan Done");
	}

	//allows for manual drive or AI pathfinding
	void FixedUpdate() {
		if (manual) 
			Manual_Update ();
		else 
			AI_Update ();
	}

	//code for self-driving of car/debugging purposes 
	void Manual_Update() {
		//avoidObject = GetComponent<ObstacleAvoid> ().avoidObstacle;
		//angleObject = GetComponent<ObstacleAvoid> ().angleObstacle;
		input_torque = Input.GetAxis("Vertical") * engineTorque * Time.deltaTime * 250.0f;
		input_steer = Input.GetAxis("Horizontal") * maxSteer;

		GetCollider(0).steerAngle = input_steer;
		GetCollider(1).steerAngle = input_steer;
		GetCollider(0).motorTorque = input_torque;
		GetCollider(1).motorTorque = input_torque;
		GetCollider(2).motorTorque = input_torque;
		GetCollider(3).motorTorque = input_torque;
		//print (angleObject);
	}

	//car driving around the waypoints
	void AI_Update () {
		//avoidObject = GetComponent<ObstacleAvoid> ().avoidObstacle;
		//angleObject = GetComponent<ObstacleAvoid> ().angleObstacle;
		//posObject = GetComponent<ObstacleAvoid> ().obstaclePosition;
		leftObj = GetComponent<ObstacleAvoid> ().leftObs;
		centerObj = GetComponent<ObstacleAvoid> ().centerObs;
		rightObj = GetComponent <ObstacleAvoid> ().rightObs;
		LeftDis = GetComponent <ObstacleAvoid> ().LeftDis;
		RightDis = GetComponent<ObstacleAvoid> ().RightDis;
		CenterDis = GetComponent <ObstacleAvoid> ().CenterDis; 
		rb.drag = rb.velocity.magnitude / 250;

		//Check if the next path segment needs to be calculated in a thread
		if (pathCalculated == false && jobInProgress == false) {
			//trigger thread job for this car to obtain the next set of waypoints
			Node pathStartNode;
			if (currentThreadJob.destinationNode == PathPlanningDataStructures.graph.endNode) {
				pathStartNode = startNode;
			} else {
				pathStartNode = currentThreadJob.destinationNode;
			}
			currentThreadJob = new DynamicPathThreadJob(pathStartNode, 
			    PathPlanningDataStructures.graph.endNode);
			currentThreadJob.Start();
			jobInProgress = true;
		}
		//Check if in progress thread has completed the path calculation
		if (jobInProgress) {
			if (currentThreadJob.isFinished()) {
				nextWayPoints = currentThreadJob.getPathWayPoints();
				pathCalculated = true;
				jobInProgress = false;
				Debug.Log ("Finished next thread job. Size: " + nextWayPoints.Count);
			}
		}
		GoToWayPoint ();

		input_rpm = (GetCollider (0).rpm + GetCollider (1).rpm) / 2 * GearRatio [CurrGear];
		ShiftGears ();

		GetCollider (0).motorTorque = engineTorque / GearRatio [CurrGear] * input_torque;
		GetCollider (1).motorTorque = engineTorque / GearRatio [CurrGear] * input_torque;
		GetCollider (2).motorTorque = engineTorque / GearRatio [CurrGear] * input_torque;
		GetCollider (3).motorTorque = engineTorque / GearRatio [CurrGear] * input_torque;
		GetCollider (0).brakeTorque = brake_power;
		GetCollider (1).brakeTorque = brake_power;
		GetCollider (2).brakeTorque = brake_power;
		GetCollider (3).brakeTorque = brake_power;

		GetCollider (0).steerAngle = maxSteer * input_steer;
		GetCollider (1).steerAngle = maxSteer * input_steer;
		//print (avoidObject);
	}

	//Determines the intial path segment for the car
	private void InitialPathPlan() {
		//first triggered thread job for this car
		startNode = PathPlanningDataStructures.graph.getClosestNode (transform.position);
		currentThreadJob = new DynamicPathThreadJob (startNode, PathPlanningDataStructures.graph.endNode);
		currentThreadJob.Start();
		currentThreadJob.Join();
		currentWayPoints = currentThreadJob.getPathWayPoints();
		//indicate that next path segment needs to calculated
		pathCalculated = false;
		jobInProgress = false;
	}

	//get the way points from the in game object
	/*private void GetWayPoints() {
		Transform[] tenativeWayPoints = wayPointObject.GetComponentsInChildren<Transform> ();
		wayPoints = new List<Transform> ();

		foreach (Transform point in tenativeWayPoints) {
			if (point.transform != wayPointObject.transform) 
				wayPoints.Add(point);
		}
	}*/

	private void ShiftGears() {
		if (input_rpm >= maxEngineRPM) {
			for (int i = 0; i < GearRatio.Length; i++) {
				if (GetCollider (0).rpm * GearRatio[i] < maxEngineRPM) {
					CurrGear = i;
					break;
				}
			}
		}

		if (input_rpm <= minEngineRPM) {
			for (int i = 0; i < GearRatio.Length; i++) {
				if (GetCollider (0).rpm * GearRatio[i] > minEngineRPM) {
					CurrGear = i;
					break; 
				}
			}
		}
	}

	//set input_steer and input_torque to move towards the way point 
	private void GoToWayPoint() {
		float steer = 0; 
		if (leftObj && !rightObj)
			steer = Mathf.Max (1.2f/LeftDis, 0.35f);
		if (rightObj && !leftObj) 
			steer = -1 * Mathf.Max (1.2f/RightDis, 0.35f);
		if (centerObj && !rightObj && !leftObj) {
			steer = Mathf.Max (1.2f/CenterDis, 0.35f); 
		}
		if (centerObj && (leftObj || rightObj)) {
			steer = steer * 1.42f; 
		}

		Vector3 travelDirection = transform.InverseTransformPoint(new Vector3 (currentWayPoints[current_point].x, transform.position.y, currentWayPoints[current_point].z));

		// For skipping if waypoint behind me
		Vector3 relPosition = transform.InverseTransformPoint (currentWayPoints [current_point]);
		if (relPosition.z <= 0) {
			int see_ahead = current_point + 1;
			//move to the next path segment
			if (see_ahead >= currentWayPoints.Count) {
				currentWayPoints = nextWayPoints;
				pathCalculated = false;
				see_ahead = 0;
			}
			Vector3 seeDirection = transform.InverseTransformPoint (new Vector3 (currentWayPoints[see_ahead].x, transform.position.y, currentWayPoints[see_ahead].z));
			if (seeDirection.z > 0) {
				print ("Skipping waypoint");
				current_point = see_ahead;
				return; 
			} 
		} 
		input_steer = travelDirection.x / travelDirection.magnitude + steer;
		if (input_steer > 1) {
			//input_steer = 1;
			input_steer = Mathf.Min (input_steer, 1.25f);
		} 
		if (input_steer < -1) {
			//input_steer = -1; 
			input_steer = Mathf.Max (input_steer, -1.25f); 
		}


		if ((input_steer > 0.35f || input_steer < -0.35f) && rb.velocity.magnitude > 7) {
			input_torque = travelDirection.z / travelDirection.magnitude - Mathf.Abs (input_steer);
		} else {
			input_torque = travelDirection.z / travelDirection.magnitude;
		}	
		
		if (travelDirection.magnitude < 12) {
			current_point ++;

			if (current_point >= currentWayPoints.Count) {
				currentWayPoints = nextWayPoints;
				pathCalculated = false;
				current_point = 0;
			}
		}

		//Debug.Log ("Current Way Point: " + current_point);
		int next_point = current_point + 1;
		if (next_point >= currentWayPoints.Count) {
			currentWayPoints = nextWayPoints;
			pathCalculated = false;
			current_point = 0;
		}
		Vector3 nextDirection = transform.InverseTransformPoint (new Vector3 (currentWayPoints[current_point].x, transform.position.y, currentWayPoints[current_point].z));
		float angle = Vector3.Angle (travelDirection, nextDirection);

		if ((leftObj || rightObj || centerObj) && (input_torque > 0) && (rb.velocity.magnitude > 1)) {
			brake_power = brakeTorque * input_torque;
		} else {
			brake_power = 0.0f;
		}
		//print (rb.velocity.sqrMagnitude);
	}

	//get each of the wheels
	private WheelCollider GetCollider(int n) {
		return wheels[n].gameObject.GetComponent<WheelCollider>();
	}

	public void OnDrawGizmosSelected() {
		if (currentWayPoints == null) {
			return; 
		}
		for (int i = 0; i < currentWayPoints.Count; i++) { 
			Vector3 point = currentWayPoints[i];
			Gizmos.color = new Color (0.0f, 0.0f, 1.0f, 0.3f);
			Gizmos.DrawCube (point, new Vector3 (5.0f, 5.0f, 5.0f));
		
			int x = i + 1;
			if (x < currentWayPoints.Count) {
				Gizmos.color = Color.magenta;
				Gizmos.DrawLine (point, currentWayPoints[x]);
			}
		}

		/*for (int i = 0; i < pathPlanner.map.meshTriangles.Length; i++) {
			Triangle currentTriangle = pathPlanner.map.meshTriangles[i];
			Gizmos.color = Color.red;
			Gizmos.DrawSphere (currentTriangle.vertex1, 0.3f);
			Gizmos.color = Color.green;
			Gizmos.DrawSphere (currentTriangle.vertex2, 0.3f);
			Gizmos.color = Color.blue;
			Gizmos.DrawSphere (currentTriangle.vertex3, 0.3f);
			Gizmos.color = Color.black;
			Gizmos.DrawLine	(currentTriangle.vertex1, currentTriangle.vertex2);
			Gizmos.DrawLine	(currentTriangle.vertex2, currentTriangle.vertex3);
			Gizmos.DrawLine	(currentTriangle.vertex1, currentTriangle.vertex3);
		} */
	}

}