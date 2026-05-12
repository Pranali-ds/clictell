/**
 * @NApiVersion 2.x
 * @NScriptType ScheduledScript
 */
define(['N/log', 'N/record', 'N/search'], function (log, record, search) {

    function execute(context) {
        try {
            var csatImportSearch = search.create({
                type: "customrecord_csat_score",
                filters: [
                    ["isinactive", "is", "F"]
                ],
                columns: [
                    search.createColumn({ name: "custrecord_employee_csat", summary: "GROUP" }),
                    search.createColumn({ name: "custrecord_year_csat", summary: "GROUP" }),
                    search.createColumn({ name: "custrecord_month_csat", summary: "GROUP" })
                ]
            });

            csatImportSearch.run().each(function (csatGroup) {
                var csatEmployee = csatGroup.getValue({ name: "custrecord_employee_csat", summary: "GROUP" });
                var csatYear = csatGroup.getValue({ name: "custrecord_year_csat", summary: "GROUP" });
                var csatMonth = csatGroup.getValue({ name: "custrecord_month_csat", summary: "GROUP" });

                var allCsatRecords = [];
                var totalCsmAvg = 0;

                var csmSearch = search.create({
                    type: "customrecord_csat_score",
                    filters: [
                        ["custrecord_employee_csat", "anyof", csatEmployee],
                        "AND", ["custrecord_year_csat", "anyof", csatYear],
                        "AND", ["custrecord_month_csat", "anyof", csatMonth],
                        "AND", ["isinactive", "is", "F"]
                    ],
                    columns: [
                        "internalid",
                        "custrecord_avg_for_a_client",
                        "custrecord_parent_client"
                    ]
                });

                csmSearch.run().each(function (res) {
                    var recObj = {
                        id: res.getValue("internalid"),
                        avg: parseFloat(res.getValue("custrecord_avg_for_a_client")) || 0,
                        parent: res.getValue("custrecord_parent_client")
                    };
                    allCsatRecords.push(recObj);
                    totalCsmAvg += recObj.avg;
                    return true;
                });

                if (allCsatRecords.length === 0) return true;

                var avgCsmScore = totalCsmAvg / allCsatRecords.length;
                // var kpiValue = avgCsmScore < 4 ? "0%" :
                //     avgCsmScore < 4.5 ? "75%" :
                //     avgCsmScore >= 4.5 ? "100%" : "125%";

              var kpiValue =
                avgCsmScore === 5 ? "125%" :
                avgCsmScore >= 4.5 ? "100%" :
                avgCsmScore >= 4 ? "75%" :
                avgCsmScore >= 3.5 ? "0%" :
                avgCsmScore >= 3 ? "-25%" :
                "-50%";

                var parentGroupMap = {};
                for (var i = 0; i < allCsatRecords.length; i++) {
                    var recData = allCsatRecords[i];
                    if (!recData.parent) continue;

                    if (!parentGroupMap[recData.parent]) {
                        parentGroupMap[recData.parent] = [];
                    }
                    parentGroupMap[recData.parent].push(recData.avg);
                }

                for (var j = 0; j < allCsatRecords.length; j++) {
                    var recDataToUpdate = allCsatRecords[j];
                    var parentId = recDataToUpdate.parent;
                    var parentAvg = null;

                    if (parentId && parentGroupMap[parentId] && parentGroupMap[parentId].length > 1) {
                        var scores = parentGroupMap[parentId];
                        var sum = 0;
                        for (var k = 0; k < scores.length; k++) {
                            sum += scores[k];
                        }
                        parentAvg = sum / scores.length;
                    }

                    var rec = record.load({
                        type: 'customrecord_csat_score',
                        id: recDataToUpdate.id
                    });

                    if (parentAvg !== null) {
                        rec.setValue({
                            fieldId: "custrecord_common_client_average",
                            value: parseFloat(parentAvg.toFixed(4))
                        });
                    }

                    rec.setValue({
                        fieldId: "custrecord_csm_average",
                        value: parseFloat(avgCsmScore.toFixed(4))
                    });

                    rec.setValue({
                        fieldId: "custrecord_kpi_achievement_csat",
                        value: kpiValue
                    });

                    var savedrec = rec.save();
                    log.debug("Updated Record", "ID: " + savedrec + ", CSM: " + csatEmployee + ", Avg: " + avgCsmScore.toFixed(4) + ", KPI: " + kpiValue);
                }

                return true;
            });
        } catch (e) {
            log.error("Error", e.message || e.toString());
        }
    }

    return {
        execute: execute
    };
});
