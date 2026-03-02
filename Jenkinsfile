def runWithOptionalApiKey(String cmd, String credentialId, String envVarName) {
  if (credentialId?.trim()) {
    withCredentials([string(credentialsId: credentialId.trim(), variable: 'INJECTED_API_KEY')]) {
      sh """#!/usr/bin/env bash
set -euo pipefail
set +x
export ${envVarName}="\${INJECTED_API_KEY}"
${cmd}
"""
    }
  } else {
    sh cmd
  }
}

pipeline {
  agent any
  parameters {
    string(name: 'GIT_SHA', defaultValue: '', description: 'Commit SHA to build')
    string(name: 'REPO_PATH', defaultValue: '', description: 'Local repo path that contains the commit')
    string(name: 'BRANCH_NAME', defaultValue: '', description: 'Branch name (informational)')
    string(name: 'API_KEY_CREDENTIAL_ID', defaultValue: '', description: 'Optional Jenkins secret text credential ID to inject at runtime')
    string(name: 'API_KEY_ENV_VAR', defaultValue: 'APP_TRACKER_API_KEY', description: 'Environment variable name for injected API key')
  }
  options {
    timestamps()
    disableConcurrentBuilds()
  }

  environment {
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
    ARTIFACT_DIR = 'artifacts'
  }

  stages {
    stage('Checkout') {
      steps {
        sh '''
          set -e
          rm -rf src
          git clone "${REPO_PATH}" src
          cd src
          git checkout "${GIT_SHA}"
        '''
      }
    }
    stage('Prep') {
      steps {
        sh 'mkdir -p "${ARTIFACT_DIR}" "${ARTIFACT_DIR}/logs"'
      }
    }

    stage('Restore') {
      steps {
        script {
          runWithOptionalApiKey(
            './scripts/ci_stage.sh restore dotnet restore Tracker.slnx',
            params.API_KEY_CREDENTIAL_ID,
            params.API_KEY_ENV_VAR ?: 'APP_TRACKER_API_KEY'
          )
        }
      }
    }

    stage('Build') {
      steps {
        script {
          runWithOptionalApiKey(
            './scripts/ci_stage.sh build dotnet build Tracker.slnx -v minimal',
            params.API_KEY_CREDENTIAL_ID,
            params.API_KEY_ENV_VAR ?: 'APP_TRACKER_API_KEY'
          )
        }
      }
    }

    stage('Tests') {
      steps {
        script {
          runWithOptionalApiKey(
            './scripts/ci_stage.sh tests dotnet test Tracker.slnx -v minimal',
            params.API_KEY_CREDENTIAL_ID,
            params.API_KEY_ENV_VAR ?: 'APP_TRACKER_API_KEY'
          )
        }
      }
    }

    stage('Deterministic Eval') {
      steps {
        script {
          runWithOptionalApiKey(
            './scripts/ci_stage.sh deterministic_eval ./scripts/run_deterministic_eval.sh',
            params.API_KEY_CREDENTIAL_ID,
            params.API_KEY_ENV_VAR ?: 'APP_TRACKER_API_KEY'
          )
        }
      }
    }

    stage('Proof Of Life') {
      steps {
        script {
          runWithOptionalApiKey(
            'SKIP_ANALYSIS=1 ./scripts/ci_stage.sh proof_of_life ./scripts/proof_of_life.sh',
            params.API_KEY_CREDENTIAL_ID,
            params.API_KEY_ENV_VAR ?: 'APP_TRACKER_API_KEY'
          )
        }
      }
    }
  }

  post {
    always {
      script {
        def result = currentBuild.currentResult ?: 'UNKNOWN'
        sh """
          if command -v jq >/dev/null 2>&1; then
            jq -s \\
              --arg buildNumber "${env.BUILD_NUMBER}" \\
              --arg jobName "${env.JOB_NAME}" \\
              --arg buildUrl "${env.BUILD_URL}" \\
              --arg result "${result}" \\
              '{
                buildNumber: (\$buildNumber | tonumber),
                jobName: \$jobName,
                buildUrl: \$buildUrl,
                result: \$result,
                stages: .,
                stageCount: length,
                totalDurationMs: (map(.durationMs) | add // 0)
              }' "${ARTIFACT_DIR}/stage-metrics.jsonl" > "${ARTIFACT_DIR}/build-metrics.json"
          fi
        """
      }

      archiveArtifacts artifacts: 'artifacts/**', allowEmptyArchive: true, fingerprint: true
    }
  }
}
