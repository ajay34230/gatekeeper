package com.teamxv.qrmonitor.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.BorderStroke
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.teamxv.qrmonitor.data.model.EventType
import com.teamxv.qrmonitor.data.model.MovementEvent
import com.teamxv.qrmonitor.data.model.SyncStatus
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable
fun NotificationsScreen(vm: MainViewModel, onBack: () -> Unit) {
    val attentionEvents by vm.attentionEvents.collectAsState(emptyList())

    Column(modifier = Modifier.fillMaxWidth()) {
        // Header
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back")
            }
            Text("NOTIFICATIONS", fontSize = 18.sp, fontWeight = FontWeight.Bold)
            Spacer(modifier = Modifier.weight(1f))
            Text("${attentionEvents.size} issue(s)", fontSize = 14.sp)
        }

        if (attentionEvents.isEmpty()) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(32.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center
            ) {
                Text("All clear!", fontSize = 16.sp, fontWeight = FontWeight.Medium)
                Text("No events requiring attention", fontSize = 12.sp, color = Color.Gray)
            }
        } else {
            LazyColumn(modifier = Modifier.fillMaxWidth()) {
                items(attentionEvents) { event ->
                    NotificationCard(event)
                }
            }
        }
    }
}

@Composable
fun NotificationCard(event: MovementEvent) {
    val flagColor = when {
        event.syncStatus == SyncStatus.REJECTED -> Color(0xFFEF4444)
        event.syncStatus == SyncStatus.CONFLICT -> Color(0xFFFB923C)
        event.flags.contains("DUPLICATE_ENTRY") -> Color(0xFFFCD34D)
        else -> Color(0xFFFCD34D)
    }

    val flagLabel = when {
        event.syncStatus == SyncStatus.REJECTED -> "Rejected"
        event.syncStatus == SyncStatus.CONFLICT -> "Conflict"
        event.flags.contains("DUPLICATE_ENTRY") -> "Duplicate Entry"
        else -> "Flagged"
    }

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(8.dp),
        shape = MaterialTheme.shapes.medium,
        color = flagColor.copy(alpha = 0.1f),
        border = BorderStroke(1.dp, flagColor)
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Filled.Warning,
                    contentDescription = "Warning",
                    tint = flagColor,
                    modifier = Modifier.padding(end = 8.dp)
                )
                Text(
                    flagLabel,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.Bold,
                    color = flagColor
                )
                Spacer(modifier = Modifier.weight(1f))
                Text(
                    formatTime(event.eventTimestamp),
                    fontSize = 11.sp,
                    color = Color.Gray
                )
            }

            Text(
                "${event.eventType.name} • ${event.entityId}",
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold,
                modifier = Modifier.padding(top = 6.dp)
            )

            if (event.remarks.isNotBlank()) {
                Text(
                    event.remarks,
                    fontSize = 12.sp,
                    color = Color.DarkGray,
                    modifier = Modifier.padding(top = 4.dp)
                )
            }

            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    "Event: ${event.eventId}",
                    fontSize = 10.sp,
                    color = Color.Gray
                )
                Text(
                    "Status: ${event.syncStatus.name}",
                    fontSize = 10.sp,
                    color = Color.Gray,
                    fontWeight = FontWeight.Medium
                )
            }
        }
    }
}

private fun formatTime(timestamp: Long): String {
    return SimpleDateFormat("HH:mm:ss", Locale.getDefault()).format(Date(timestamp))
}
