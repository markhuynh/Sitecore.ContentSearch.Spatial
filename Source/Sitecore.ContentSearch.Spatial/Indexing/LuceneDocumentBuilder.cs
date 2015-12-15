﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Lucene.Net.Documents;
using Lucene.Net.Spatial;
using Lucene.Net.Spatial.Prefix;
using Lucene.Net.Spatial.Prefix.Tree;
using Lucene.Net.Spatial.Vector;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.LuceneProvider;
using Sitecore.Data;
using Sitecore.Data.Items;
using Spatial4n.Core.Context;
using Spatial4n.Core.Shapes;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.ContentSearch.Spatial.Configurations;
using System.Xml;
using Sitecore.Xml;

namespace Sitecore.ContentSearch.Spatial.Indexing
{
    public class LuceneSpatialDocumentBuilder : LuceneDocumentBuilder
    {
        public static int precisionStep;
        private static SpatialConfigurations spatialConfigurations;
        public LuceneSpatialDocumentBuilder(IIndexable indexable, IProviderUpdateContext context)
            : base(indexable, context)
        {
            if (spatialConfigurations == null)
            {
                BuildSettings();
            }
        }

        public override void AddItemFields()
        {
            
            Item item = (Item)(this.Indexable as SitecoreIndexableItem);
            if (item != null && spatialConfigurations.LocationSettings.Where(i=>i.TemplateId.Equals( item.TemplateID)).Any())
            {
                AddPoint(item).ForEach(i => base.CollectedFields.Enqueue(i));
            }
            //Integration Geo Location data type
            else if (
                item.Template != null &&
                (item.Template.ID.Equals(ID.Parse(Sitecore.ContentSearch.Spatial.Common.Constants.GeoLocationTemplateId)) 
                || item.Template.BaseTemplates.Any(i => i.ID.Equals(ID.Parse(Common.Constants.GeoLocationTemplateId))))
                )
            {
                var geoLocationValue = item[Sitecore.ContentSearch.Spatial.Common.Constants.GeoLocationFieldName];
                if(!string.IsNullOrWhiteSpace(geoLocationValue) && geoLocationValue.IndexOf(',')>=0)
                {
                    string ltdString =  geoLocationValue.Split(',')[0];
                    string lngString = geoLocationValue.Split(',')[1];
                    double ltd = 0;
                    double lng = 0;
                    double.TryParse(ltdString, out ltd);
                    double.TryParse(lngString, out lng);
                    if(ltd!=0 && lng != 0)
                    {
                        AddPoint(lng, ltd).ForEach(i => base.CollectedFields.Enqueue(i));
                    }
                }
            }
            base.AddItemFields();
        }

        private List<IFieldable> AddPoint(Item item)
        {
            List<IFieldable> pointFields = new List<IFieldable>();
            var setting = spatialConfigurations.LocationSettings.Where(i => i.TemplateId.Equals(item.TemplateID)).FirstOrDefault();
            if (setting == null)
                return pointFields;

            SpatialContext ctx =  SpatialContext.GEO;
             
            SpatialPrefixTree grid = new GeohashPrefixTree(ctx, 11);
            var strategy = new PointVectorStrategy(ctx, Sitecore.ContentSearch.Spatial.Common.Constants.LocationFieldName);
            
            double lng = 0;
            double lat = 0;
            bool parsedLat = false;
            bool parsedLong = false;

            if (!string.IsNullOrEmpty(item[setting.LatitudeField]))
            {
                parsedLat = Double.TryParse(item[setting.LatitudeField], out lat);
            }

            if (!string.IsNullOrEmpty(item[setting.LongitudeField]))
            {
                parsedLong = Double.TryParse(item[setting.LongitudeField], out lng);
            }
            if (!parsedLat && !parsedLong)
                return pointFields;

            pointFields = AddPoint( lng, lat);
            
            return pointFields;
        }

        private static List<IFieldable> AddPoint(double lng, double lat)
        {
            SpatialContext ctx =  SpatialContext.GEO;
             
            var strategy = new PointVectorStrategy(ctx, Sitecore.ContentSearch.Spatial.Common.Constants.LocationFieldName);//var strategy = new PrefixTreeStrategy(ctx, Sitecore.ContentSearch.Spatial.Common.Constants.LocationFieldName);
             
            List<IFieldable> pointFields = new List<IFieldable>();
            Point shape = ctx.MakePoint(lng, lat);
            foreach (var f in strategy.CreateIndexableFields(shape))
            {
                if (f != null)
                {
                    pointFields.Add(f);
                }
            }
            return pointFields;
        }

        private void BuildSettings()
        {
            precisionStep = 8;
            spatialConfigurations = new SpatialConfigurations();
            spatialConfigurations.LocationSettings = new List<LocationSettings>();
            XmlNodeList configs = Factory.GetConfigNodes("contentSearchSpatial/IncludeTemplates/Template");

            if (configs == null)
            {
                Log.Warn("sitecore/contentSearchSpatial/IncludeTemplates/Template node was not defined; Please include the Sitecore.ContentSearch.Spatial.config file in include folder.", this);
                return;
            }
            foreach(XmlNode node in configs)
            {
                string templateId = XmlUtil.GetAttribute("id", node);
                string latitudeField = XmlUtil.GetAttribute("LatitudeField", node);
                string longitudeField = XmlUtil.GetAttribute("LongitudeField", node);

                LocationSettings locationSetting = new LocationSettings();
                locationSetting.LatitudeField = latitudeField;
                locationSetting.LongitudeField = longitudeField;
                
                if(ID.IsID(templateId))
                {
                    locationSetting.TemplateId = ID.Parse(templateId);
                }
                spatialConfigurations.LocationSettings.Add(locationSetting);
            }
        }
    }
}
