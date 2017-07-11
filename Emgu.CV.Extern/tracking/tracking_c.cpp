//----------------------------------------------------------------------------
//
//  Copyright (C) 2004-2017 by EMGU Corporation. All rights reserved.
//
//----------------------------------------------------------------------------

#include "tracking_c.h"

/*
cv::Tracker* cveTrackerCreate(cv::String* trackerType)
{
   cv::Ptr<cv::Tracker> tracker = cv::Tracker::create(*trackerType);
   tracker.addref(); 
   return tracker.get();
}*/
bool cveTrackerInit(cv::Tracker* tracker, cv::Mat* image, CvRect* boundingBox)
{
   return tracker->init(*image, *boundingBox);
}
bool cveTrackerUpdate(cv::Tracker* tracker, cv::Mat* image, CvRect* boundingBox)
{
   cv::Rect2d box;
   bool result = tracker->update(*image, box);
   *boundingBox = box;
   return result;
}
/*
void cveTrackerRelease(cv::Tracker** tracker)
{
   delete *tracker;
   *tracker = 0;
}
*/

cv::TrackerBoosting* cveTrackerBoostingCreate(int numClassifiers, float samplerOverlap, float samplerSearchFactor, int iterationInit, int featureSetNumFeatures, cv::Tracker** tracker)
{
	cv::TrackerBoosting::Params p;
	p.numClassifiers = numClassifiers;
	p.samplerOverlap = samplerOverlap;
	p.samplerSearchFactor = samplerSearchFactor;
	p.iterationInit = iterationInit;
	p.featureSetNumFeatures = featureSetNumFeatures;
	cv::Ptr<cv::TrackerBoosting> ptr = cv::TrackerBoosting::create(p);
	ptr.addref();
	*tracker = static_cast<cv::Tracker*>(ptr.get());
	return ptr.get();
}
void cveTrackerBoostingRelease(cv::TrackerBoosting** tracker)
{
	delete *tracker;
	*tracker = 0;
}

cv::TrackerMedianFlow* cveTrackerMedianFlowCreate(int pointsInGrid, CvSize* winSize, int maxLevel, CvTermCriteria* termCriteria, CvSize* winSizeNCC, double maxMedianLengthOfDisplacementDifference, cv::Tracker** tracker)
{
	cv::TrackerMedianFlow::Params p;
	p.pointsInGrid = pointsInGrid;
	p.winSize = *winSize;
	p.maxLevel = maxLevel;
	p.termCriteria = *termCriteria;
	p.winSizeNCC = *winSizeNCC;
	p.maxMedianLengthOfDisplacementDifference = maxMedianLengthOfDisplacementDifference;

	cv::Ptr<cv::TrackerMedianFlow> ptr = cv::TrackerMedianFlow::create(p);
	ptr.addref();
	*tracker = static_cast<cv::Tracker*>(ptr.get());
	return ptr.get();
}
void cveTrackerMedianFlowRelease(cv::TrackerMedianFlow** tracker)
{
	delete* tracker;
	*tracker = 0;
}

cv::MultiTracker* cveMultiTrackerCreate()
{
   return new cv::MultiTracker();
}
bool cveMultiTrackerAdd(cv::MultiTracker* multiTracker, cv::Tracker* tracker, cv::Mat* image, CvRect* boundingBox)
{
   return multiTracker->add(tracker, *image, *boundingBox);
}

bool cveMultiTrackerUpdate(cv::MultiTracker* tracker, cv::Mat* image, std::vector<CvRect>* boundingBox)
{
   std::vector<cv::Rect2d> bb;
   bool result = tracker->update(*image, bb);
   boundingBox->clear();
   for (std::vector<cv::Rect2d>::iterator it = bb.begin(); it != bb.end(); ++it)
   {
      boundingBox->push_back(*it);
   }
   return result;
}
void cveMultiTrackerRelease(cv::MultiTracker** tracker)
{
   delete* tracker;
   *tracker = 0;
}