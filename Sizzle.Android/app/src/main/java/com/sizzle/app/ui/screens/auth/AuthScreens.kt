package com.sizzle.app.ui.screens.auth

import android.app.Activity
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.outlined.Visibility
import androidx.compose.material.icons.outlined.VisibilityOff
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.auth.GoogleSignIn
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.message
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.ConfirmEmailRequest
import com.sizzle.app.data.model.GoogleTokenRequest
import com.sizzle.app.data.model.LoginRequest
import com.sizzle.app.data.model.LoginResult
import com.sizzle.app.data.model.RegisterRequest
import com.sizzle.app.data.model.UserIdRequest
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.FieldShape
import com.sizzle.app.ui.components.GoogleButton
import com.sizzle.app.ui.components.OrDivider
import com.sizzle.app.ui.components.PrimaryButton
import com.sizzle.app.ui.components.sizzleFieldColors
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.launch

/** Login, registration and email confirmation. One ViewModel per screen instance. */
class AuthViewModel(private val container: AppContainer) : ViewModel() {
    var busy by mutableStateOf(false)
        private set
    var googleBusy by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    /** Message for a failed Google sign-in on the device (no account, Play services issue). */
    var googleProblem by mutableStateOf<String?>(null)
        private set
    var info by mutableStateOf(false)
        private set

    fun login(email: String, password: String, done: (LoginResult) -> Unit) = run {
        container.api.login(LoginRequest(email.trim(), password)).also { finish(it, done) }
    }

    fun register(name: String, email: String, password: String, done: (String) -> Unit) = run {
        val result = container.api.register(RegisterRequest(email.trim(), name.trim(), password))
        done(result.userId)
    }

    fun confirm(userId: String, code: String, done: () -> Unit) = run {
        val result = container.api.confirmEmail(ConfirmEmailRequest(userId, code.trim()))
        container.session.signIn(result.jwtToken)
        done()
    }

    fun resend(userId: String) = run {
        container.api.resendConfirmEmail(UserIdRequest(userId))
        info = true
    }

    fun google(activity: Activity, done: (LoginResult) -> Unit) {
        googleBusy = true
        failure = null
        googleProblem = null
        viewModelScope.launch {
            try {
                val token = GoogleSignIn.idToken(activity)
                if (token != null) {
                    runApi { container.api.loginWithGoogle(GoogleTokenRequest(token)) }
                        .onSuccess { finish(it, done) }
                        .onFailure { failure = it as ApiFailure }
                }
            } catch (e: kotlinx.coroutines.CancellationException) {
                throw e
            } catch (e: Exception) {
                googleProblem = activity.getString(R.string.auth_google_failed) + (e.message?.let { ": $it" } ?: "")
            }
            googleBusy = false
        }
    }

    private suspend fun finish(result: LoginResult, done: (LoginResult) -> Unit) {
        result.jwtToken?.let { container.session.signIn(it) }
        done(result)
    }

    private fun run(block: suspend () -> Unit) {
        busy = true
        failure = null
        info = false
        viewModelScope.launch {
            runApi { block() }.onFailure { failure = it as ApiFailure }
            busy = false
        }
    }
}

/** Branded header standing in for the website's split-screen image panel. */
@Composable
private fun AuthScaffold(navigator: Navigator, title: String, content: @Composable ColumnScope.() -> Unit) {
    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState()).imePadding()) {
        Box(
            Modifier
                .fillMaxWidth()
                .background(Brush.linearGradient(listOf(Color(0xFFFF8A3D), Color(0xFFFF6A2B), Color(0xFFD9381E))))
                .statusBarsPadding()
                .padding(start = 8.dp, end = 24.dp, top = 4.dp, bottom = 28.dp),
        ) {
            IconButton(onClick = navigator::back) { Icon(Icons.AutoMirrored.Filled.ArrowBack, stringResource(R.string.nav_back), tint = Color.White) }
            Column(Modifier.padding(start = 16.dp, top = 52.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Box(
                    Modifier.size(64.dp).clip(RoundedCornerShape(18.dp)).background(Color.White.copy(alpha = 0.18f)),
                    contentAlignment = Alignment.Center,
                ) {
                    Image(painterResource(R.drawable.logo_mark), null, Modifier.size(44.dp), colorFilter = ColorFilter.tint(Color.White))
                }
                Text(stringResource(R.string.auth_hero_title), color = Color.White, style = MaterialTheme.typography.headlineMedium)
                Text(stringResource(R.string.auth_hero_subtitle), color = Color.White.copy(alpha = 0.9f))
            }
        }
        Column(Modifier.fillMaxWidth().padding(24.dp)) {
            Text(title, style = MaterialTheme.typography.headlineLarge, modifier = Modifier.padding(bottom = 20.dp))
            content()
        }
    }
}

@Composable
private fun AuthField(
    value: String,
    onChange: (String) -> Unit,
    label: String,
    keyboardType: KeyboardType = KeyboardType.Text,
    password: Boolean = false,
) {
    var visible by rememberSaveable { mutableStateOf(false) }
    OutlinedTextField(
        value = value,
        onValueChange = onChange,
        label = { Text(label) },
        singleLine = true,
        modifier = Modifier.fillMaxWidth().padding(bottom = 12.dp),
        shape = FieldShape,
        colors = sizzleFieldColors(),
        keyboardOptions = KeyboardOptions(keyboardType = if (password) KeyboardType.Password else keyboardType),
        visualTransformation = if (password && !visible) PasswordVisualTransformation() else VisualTransformation.None,
        trailingIcon = if (password) {
            { IconButton(onClick = { visible = !visible }) { Icon(if (visible) Icons.Outlined.VisibilityOff else Icons.Outlined.Visibility, null) } }
        } else {
            null
        },
    )
}

@Composable
private fun SwitchLink(question: String, action: String, onClick: () -> Unit) {
    Row(Modifier.fillMaxWidth().padding(top = 24.dp), horizontalArrangement = Arrangement.Center) {
        Text(question, color = Sizzle.colors.muted)
        Text(
            action,
            color = Sizzle.colors.accentText,
            fontWeight = FontWeight.SemiBold,
            modifier = Modifier.padding(start = 6.dp).clickable(onClick = onClick),
        )
    }
}

@Composable
private fun GoogleSection(vm: AuthViewModel, navigator: Navigator) {
    if (!GoogleSignIn.enabled) return
    val activity = LocalContext.current as Activity
    GoogleButton(onClick = { vm.google(activity) { result -> afterLogin(result, navigator) } }, busy = vm.googleBusy)
    vm.googleProblem?.let { AlertBox(it, Modifier.padding(top = 8.dp)) }
    OrDivider(stringResource(R.string.auth_or))
}

private fun afterLogin(result: LoginResult, navigator: Navigator) {
    if (result.jwtToken != null) navigator.finishAuth() else navigator.confirmEmail(result.userId)
}

@Composable
fun LoginScreen(navigator: Navigator) {
    val vm = appViewModel(key = "login") { AuthViewModel(it) }
    var email by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }
    AuthScaffold(navigator, stringResource(R.string.auth_login_title)) {
        GoogleSection(vm, navigator)
        AuthField(email, { email = it }, stringResource(R.string.auth_email), KeyboardType.Email)
        AuthField(password, { password = it }, stringResource(R.string.auth_password), password = true)
        vm.failure?.let { AlertBox(it.message(), Modifier.padding(bottom = 12.dp)) }
        PrimaryButton(
            stringResource(R.string.auth_login_submit),
            { vm.login(email, password) { afterLogin(it, navigator) } },
            Modifier.fillMaxWidth(),
            enabled = email.isNotBlank() && password.isNotEmpty(),
            busy = vm.busy,
        )
        SwitchLink(stringResource(R.string.auth_login_no_account), stringResource(R.string.auth_login_sign_up), navigator::register)
    }
}

@Composable
fun RegisterScreen(navigator: Navigator) {
    val vm = appViewModel(key = "register") { AuthViewModel(it) }
    var name by rememberSaveable { mutableStateOf("") }
    var email by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }
    AuthScaffold(navigator, stringResource(R.string.auth_register_title)) {
        GoogleSection(vm, navigator)
        AuthField(name, { name = it }, stringResource(R.string.auth_name))
        AuthField(email, { email = it }, stringResource(R.string.auth_email), KeyboardType.Email)
        AuthField(password, { password = it }, stringResource(R.string.auth_password), password = true)
        vm.failure?.let { AlertBox(it.message(), Modifier.padding(bottom = 12.dp)) }
        PrimaryButton(
            stringResource(R.string.auth_register_submit),
            { vm.register(name, email, password, navigator::confirmEmail) },
            Modifier.fillMaxWidth(),
            enabled = name.isNotBlank() && email.isNotBlank() && password.isNotEmpty(),
            busy = vm.busy,
        )
        SwitchLink(stringResource(R.string.auth_register_have_account), stringResource(R.string.auth_register_log_in), navigator::login)
    }
}

@Composable
fun ConfirmEmailScreen(userId: String, navigator: Navigator) {
    val vm = appViewModel(key = "confirm-$userId") { AuthViewModel(it) }
    var code by rememberSaveable { mutableStateOf("") }
    AuthScaffold(navigator, stringResource(R.string.auth_confirm_title)) {
        Text(stringResource(R.string.auth_confirm_intro), color = Sizzle.colors.muted, modifier = Modifier.padding(bottom = 16.dp))
        AuthField(code, { code = it }, stringResource(R.string.auth_confirm_code), KeyboardType.Number)
        vm.failure?.let { AlertBox(it.message(), Modifier.padding(bottom = 12.dp)) }
        if (vm.info) {
            Text(stringResource(R.string.auth_confirm_resent), color = Sizzle.colors.muted, modifier = Modifier.padding(bottom = 12.dp))
        }
        PrimaryButton(
            stringResource(R.string.auth_confirm_submit),
            { vm.confirm(userId, code, navigator::finishAuth) },
            Modifier.fillMaxWidth(),
            enabled = code.isNotBlank(),
            busy = vm.busy,
        )
        Text(
            stringResource(R.string.auth_confirm_resend),
            color = Sizzle.colors.accentText,
            fontWeight = FontWeight.SemiBold,
            modifier = Modifier.padding(top = 20.dp).clickable { vm.resend(userId) },
        )
    }
}
